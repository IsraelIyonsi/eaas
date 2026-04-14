namespace EaaS.Domain.Providers;

/// <summary>
/// Canonical post-normalization email event vocabulary. Provider-specific vocabularies
/// (SES <c>Bounce</c>+<c>Permanent</c>, Mailgun <c>failed</c>+<c>severity=permanent</c>, etc.)
/// map to these values via <see cref="IEmailEventNormalizer"/>. Numeric values preserve
/// the SES-era <see cref="EaaS.Domain.Enums.EventType"/> ordering so downstream consumers
/// that persist the enum integer are not affected.
/// </summary>
public enum EmailEventType
{
    Accepted     = 0,
    Delivered    = 1,
    Bounced      = 2,
    TempFailed   = 3,
    PermFailed   = 4,
    Complained   = 5,
    Opened       = 6,
    Clicked      = 7,
    Unsubscribed = 8,
    Stored       = 9
}

public sealed record ProviderEmailEvent(
    string ProviderKey,
    string ProviderMessageId,
    EmailEventType Type,
    DateTimeOffset OccurredAt,
    string? Recipient,
    string? DiagnosticCode,
    IReadOnlyDictionary<string, string> ProviderMetadata);
