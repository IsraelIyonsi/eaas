using EaaS.Domain.Providers;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.EmailProviders.Configuration;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EaaS.Infrastructure.EmailProviders.Providers.Smtp;

/// <summary>
/// SMTP adapter (local dev via Mailpit) behind <see cref="IEmailProvider"/>. Maintains
/// Phase 0 parity with the pre-abstraction <c>SmtpEmailService</c>.
/// </summary>
public sealed partial class SmtpEmailProvider : IEmailProvider
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(IOptions<SmtpSettings> settings, ILogger<SmtpEmailProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public string ProviderKey => EmailProviderConfigKeys.ProviderKeys.Smtp;

    public EmailProviderCapability Capabilities =>
        EmailProviderCapability.Attachments | EmailProviderCapability.SendRaw;

    public async Task<EmailSendOutcome> SendAsync(
        SendEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.To is null || request.To.Count == 0)
            throw new EmailValidationException("At least one recipient is required.");
        if (string.IsNullOrWhiteSpace(request.From))
            throw new EmailValidationException("Sender address is required.");

        try
        {
            var message = new MimeMessage();
            var from = string.IsNullOrWhiteSpace(request.FromName)
                ? MailboxAddress.Parse(request.From)
                : new MailboxAddress(request.FromName, request.From);
            message.From.Add(from);

            foreach (var to in request.To)
                message.To.Add(MailboxAddress.Parse(to));

            if (request.Cc is { Count: > 0 })
                foreach (var cc in request.Cc) message.Cc.Add(MailboxAddress.Parse(cc));

            if (request.Bcc is { Count: > 0 })
                foreach (var bcc in request.Bcc) message.Bcc.Add(MailboxAddress.Parse(bcc));

            message.Subject = request.Subject;

            var bodyBuilder = new BodyBuilder();
            if (request.HtmlBody is not null) bodyBuilder.HtmlBody = request.HtmlBody;
            if (request.TextBody is not null) bodyBuilder.TextBody = request.TextBody;
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, cancellationToken);

            if (_settings.Username is not null && _settings.Password is not null)
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            var messageId = message.MessageId ?? Guid.NewGuid().ToString();
            LogEmailSent(_logger, messageId);

            return new EmailSendOutcome(true, messageId, null, null, false);
        }
        catch (Exception ex)
        {
            LogEmailSendFailed(_logger, ex);
            return new EmailSendOutcome(false, null, ex.GetType().Name, ex.Message, true);
        }
    }

    public async Task<EmailSendOutcome> SendRawAsync(
        SendRawEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MimeMessage is null)
            throw new EmailValidationException("MIME message stream is required.");

        try
        {
            var message = await MimeMessage.LoadAsync(request.MimeMessage, cancellationToken);

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, cancellationToken);

            if (_settings.Username is not null && _settings.Password is not null)
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            var messageId = message.MessageId ?? Guid.NewGuid().ToString();
            LogRawEmailSent(_logger, messageId);

            return new EmailSendOutcome(true, messageId, null, null, false);
        }
        catch (Exception ex)
        {
            LogRawEmailSendFailed(_logger, ex);
            return new EmailSendOutcome(false, null, ex.GetType().Name, ex.Message, true);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent via SMTP (Mailpit), MessageId: {MessageId}")]
    private static partial void LogEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email via SMTP")]
    private static partial void LogEmailSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Raw email sent via SMTP (Mailpit), MessageId: {MessageId}")]
    private static partial void LogRawEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send raw email via SMTP")]
    private static partial void LogRawEmailSendFailed(ILogger logger, Exception ex);
}
