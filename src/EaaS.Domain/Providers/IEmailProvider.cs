namespace EaaS.Domain.Providers;

/// <summary>
/// Outbound email provider abstraction. Every provider adapter (SES, Mailgun,
/// SMTP/Mailpit, future SparkPost/Postmark) implements this interface. Both
/// <see cref="SendAsync"/> and <see cref="SendRawAsync"/> must be preserved —
/// <c>SendEmailConsumer</c> switches to the raw path when custom headers
/// (RFC 2369 List-Unsubscribe, RFC 8058 One-Click) or attachments are required.
/// </summary>
public interface IEmailProvider
{
    /// <summary>Stable provider key (e.g. <c>"ses"</c>, <c>"mailgun"</c>, <c>"smtp"</c>).</summary>
    string ProviderKey { get; }

    /// <summary>Declarative capabilities so callers can branch without <c>is</c>/<c>switch</c>.</summary>
    EmailProviderCapability Capabilities { get; }

    Task<EmailSendOutcome> SendAsync(
        SendEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<EmailSendOutcome> SendRawAsync(
        SendRawEmailRequest request,
        CancellationToken cancellationToken = default);
}
