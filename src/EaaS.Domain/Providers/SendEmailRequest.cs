namespace EaaS.Domain.Providers;

/// <summary>
/// Provider-agnostic outbound send request. Shape is the union of the architect
/// doc's SendEmailRequest and the Mailgun-Flex-specific fields (<see cref="SendingDomain"/>
/// and <see cref="CustomVariables"/>) — Flex has no subaccount isolation, so tenant
/// routing depends on a verified sending domain plus the <c>v:tenant_id</c> variable.
/// </summary>
public sealed record SendEmailRequest(
    Guid TenantId,
    string From,
    string? FromName,
    IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    IReadOnlyDictionary<string, string>? CustomHeaders = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, string>? CustomVariables = null,
    string? SendingDomain = null,
    string? ConfigurationSetName = null,
    string? IdempotencyKey = null);

/// <summary>Raw-MIME outbound send request — used for custom headers + attachments.</summary>
public sealed record SendRawEmailRequest(
    Guid TenantId,
    Stream MimeMessage,
    string? SendingDomain = null,
    IReadOnlyDictionary<string, string>? CustomVariables = null,
    string? ConfigurationSetName = null,
    string? IdempotencyKey = null);
