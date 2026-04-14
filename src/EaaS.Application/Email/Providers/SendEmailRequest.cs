namespace EaaS.Application.Email.Providers;

/// <summary>Provider-agnostic send request. Immutable.</summary>
public sealed record SendEmailRequest
{
    public required string From { get; init; }
    public string? FromName { get; init; }
    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();
    public string? ReplyTo { get; init; }
    public required string Subject { get; init; }
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = Array.Empty<EmailAttachment>();
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> CustomVariables { get; init; }
        = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Headers { get; init; }
        = new Dictionary<string, string>();
}

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    ReadOnlyMemory<byte> Content,
    bool IsInline = false,
    string? ContentId = null);
