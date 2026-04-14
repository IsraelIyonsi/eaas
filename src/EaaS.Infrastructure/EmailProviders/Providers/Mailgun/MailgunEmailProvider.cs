using EaaS.Domain.Providers;
using EaaS.Infrastructure.EmailProviders.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendNex.Mailgun;
using SendNex.Mailgun.Dtos;

namespace EaaS.Infrastructure.EmailProviders.Providers.Mailgun;

/// <summary>
/// <see cref="IEmailProvider"/> adapter translating the provider-agnostic
/// <see cref="SendEmailRequest"/> / <see cref="SendRawEmailRequest"/> into Mailgun
/// v3 calls. Flex launch-tier (shared account, no subaccount isolation) — every
/// outbound message is tagged with <c>v:tenant_id=&lt;id&gt;</c> for attribution
/// and webhook routing. No Mailgun DTO leaks above this class.
/// </summary>
public sealed partial class MailgunEmailProvider : IEmailProvider
{
    private readonly IMailgunClient _client;
    private readonly MailgunOptions _options;
    private readonly ILogger<MailgunEmailProvider> _logger;

    public MailgunEmailProvider(
        IMailgunClient client,
        IOptions<MailgunOptions> options,
        ILogger<MailgunEmailProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderKey => MailgunProviderKey.Value;

    public EmailProviderCapability Capabilities =>
        EmailProviderCapability.Attachments
        | EmailProviderCapability.Tags
        | EmailProviderCapability.CustomVariables
        | EmailProviderCapability.SendRaw;

    public async Task<EmailSendOutcome> SendAsync(
        SendEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.To is null || request.To.Count == 0)
            throw new EmailValidationException("At least one recipient is required.");
        if (string.IsNullOrWhiteSpace(request.From))
            throw new EmailValidationException("Sender address is required.");

        var domain = ResolveSendingDomain(request.SendingDomain);

        var mailgunRequest = new MailgunSendRequest
        {
            Domain = domain,
            From = FormatFrom(request.From, request.FromName),
            To = request.To,
            Cc = request.Cc,
            Bcc = request.Bcc,
            Subject = request.Subject,
            Text = request.TextBody,
            Html = request.HtmlBody,
            Tags = request.Tags,
            CustomHeaders = request.CustomHeaders,
            CustomVariables = MergeTenantVariable(request.TenantId, request.CustomVariables),
            Attachments = MapAttachments(request.Attachments),
        };

        try
        {
            var response = await _client.SendAsync(mailgunRequest, cancellationToken).ConfigureAwait(false);
            LogEmailSent(_logger, response.Id ?? "(missing)");
            return new EmailSendOutcome(true, response.Id, null, null, IsRetryable: false);
        }
        catch (MailgunException ex)
        {
            LogEmailSendFailed(_logger, ex);
            return new EmailSendOutcome(false, null, BuildErrorCode(ex), ex.Message, ex.IsRetryable);
        }
    }

    public async Task<EmailSendOutcome> SendRawAsync(
        SendRawEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MimeMessage is null)
            throw new EmailValidationException("MIME message stream is required.");

        var domain = ResolveSendingDomain(request.SendingDomain);
        var variables = MergeTenantVariable(request.TenantId, request.CustomVariables);

        try
        {
            var response = await _client
                .SendRawAsync(domain, request.MimeMessage, variables, cancellationToken)
                .ConfigureAwait(false);
            LogRawEmailSent(_logger, response.Id ?? "(missing)");
            return new EmailSendOutcome(true, response.Id, null, null, IsRetryable: false);
        }
        catch (MailgunException ex)
        {
            LogRawEmailSendFailed(_logger, ex);
            return new EmailSendOutcome(false, null, BuildErrorCode(ex), ex.Message, ex.IsRetryable);
        }
    }

    private string ResolveSendingDomain(string? requestDomain)
    {
        var domain = requestDomain ?? _options.DefaultSendingDomain;
        if (string.IsNullOrWhiteSpace(domain))
            throw new EmailValidationException(
                "Mailgun sending domain is required. Provide SendEmailRequest.SendingDomain " +
                "or configure EmailProviders:Mailgun:DefaultSendingDomain.");
        return domain;
    }

    private static string FormatFrom(string address, string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? address : $"{displayName} <{address}>";

    private static Dictionary<string, string> MergeTenantVariable(
        Guid tenantId,
        IReadOnlyDictionary<string, string>? custom)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        if (custom is not null)
            foreach (var kv in custom) merged[kv.Key] = kv.Value;

        // Flex mode: tenant attribution lives exclusively in the v:tenant_id variable.
        merged[MailgunConstants.CustomVariables.TenantId] =
            tenantId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        return merged;
    }

    private static List<MailgunAttachment>? MapAttachments(
        IReadOnlyList<EmailAttachment>? source)
    {
        if (source is null || source.Count == 0) return null;
        var list = new List<MailgunAttachment>(source.Count);
        foreach (var a in source)
            list.Add(new MailgunAttachment(a.Filename, a.ContentType, a.Content));
        return list;
    }

    private static string BuildErrorCode(MailgunException ex) =>
        ex.StatusCode is int status
            ? $"mailgun_http_{status}"
            : "mailgun_transport_error";

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent via Mailgun, MessageId: {MessageId}")]
    private static partial void LogEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email via Mailgun")]
    private static partial void LogEmailSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Raw email sent via Mailgun, MessageId: {MessageId}")]
    private static partial void LogRawEmailSent(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send raw email via Mailgun")]
    private static partial void LogRawEmailSendFailed(ILogger logger, Exception ex);
}
