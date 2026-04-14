namespace EaaS.Application.Email.Providers;

public enum EmailEventType
{
    Unknown = 0,
    Delivered,
    PermanentFailure,
    TemporaryFailure,
    Complained,
    Clicked,
    Opened,
    Unsubscribed,
    Rejected,
    Deferred,
}

/// <summary>Normalized, provider-agnostic webhook event.</summary>
public sealed record EmailEvent
{
    public required string ProviderName { get; init; }
    public required string ProviderMessageId { get; init; }
    public required EmailEventType Type { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? Recipient { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Original payload bytes, retained verbatim for forensics / replay.</summary>
    public required ReadOnlyMemory<byte> RawPayload { get; init; }
}
