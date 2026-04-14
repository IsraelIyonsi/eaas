using EaaS.Domain.Interfaces;
using EaaS.Domain.Providers;
using EaaS.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

/// <summary>
/// Sends password reset emails through the same provider pipeline used for customer mail
/// (dogfooding), but bypasses the tenant-domain verification check — this is a system email
/// from the SendNex-operations sender.
/// </summary>
public sealed partial class PasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IEmailProviderFactory _providerFactory;
    private readonly PasswordResetSettings _settings;
    private readonly ILogger<PasswordResetEmailSender> _logger;

    public PasswordResetEmailSender(
        IEmailProviderFactory providerFactory,
        IOptions<PasswordResetSettings> settings,
        ILogger<PasswordResetEmailSender> logger)
    {
        _providerFactory = providerFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendResetEmailAsync(string recipientEmail, string token, CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.DashboardBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
        var expiryMinutes = _settings.TokenLifetimeMinutes;

        var subject = "Reset your SendNex password";
        var htmlBody = BuildHtmlBody(link, expiryMinutes);
        var textBody = BuildTextBody(link, expiryMinutes);

        // System email — no tenant scoping. Use Guid.Empty to resolve the platform default adapter.
        var provider = _providerFactory.GetForTenant(Guid.Empty);

        var request = new SendEmailRequest(
            TenantId: Guid.Empty,
            From: _settings.SystemSender,
            FromName: string.IsNullOrWhiteSpace(_settings.SystemSenderName) ? null : _settings.SystemSenderName,
            To: new[] { recipientEmail },
            Cc: null,
            Bcc: null,
            Subject: subject,
            HtmlBody: htmlBody,
            TextBody: textBody);

        var outcome = await provider.SendAsync(request, cancellationToken);

        if (!outcome.Success)
        {
            LogSendFailed(_logger, recipientEmail, outcome.ErrorMessage ?? "unknown error");
        }
        else
        {
            LogSendSucceeded(_logger, recipientEmail);
        }
    }

    private static string BuildHtmlBody(string link, int expiryMinutes) => $@"<!doctype html>
<html>
  <body style=""font-family: Arial, Helvetica, sans-serif; color: #111; max-width: 560px; margin: 0 auto; padding: 24px;"">
    <h1 style=""font-size: 20px; margin-bottom: 16px;"">Reset your SendNex password</h1>
    <p>We received a request to reset the password on your SendNex account. Click the button below to choose a new password:</p>
    <p style=""margin: 24px 0;"">
      <a href=""{link}"" style=""background: #0ea5e9; color: #fff; padding: 12px 20px; text-decoration: none; border-radius: 6px; display: inline-block;"">Reset password</a>
    </p>
    <p>Or paste this link into your browser:</p>
    <p style=""word-break: break-all; color: #555;""><a href=""{link}"">{link}</a></p>
    <p style=""color: #555; font-size: 13px;"">This link will expire in {expiryMinutes} minutes and can only be used once.</p>
    <p style=""color: #555; font-size: 13px;"">If you didn&rsquo;t request a password reset, you can safely ignore this email — your password will stay the same.</p>
    <hr style=""border: none; border-top: 1px solid #eee; margin: 24px 0;"" />
    <p style=""color: #888; font-size: 12px;"">SendNex — email infrastructure for developers.</p>
  </body>
</html>";

    private static string BuildTextBody(string link, int expiryMinutes) => $@"Reset your SendNex password

We received a request to reset the password on your SendNex account.
Open this link to choose a new password:

{link}

This link will expire in {expiryMinutes} minutes and can only be used once.

If you didn't request a password reset, you can safely ignore this email — your password will stay the same.

— SendNex
";

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset email sent to {Email}")]
    private static partial void LogSendSucceeded(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send password reset email to {Email}: {Error}")]
    private static partial void LogSendFailed(ILogger logger, string email, string error);
}
