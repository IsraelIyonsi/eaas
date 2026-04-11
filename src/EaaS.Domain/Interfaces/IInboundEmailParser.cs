namespace EaaS.Domain.Interfaces;

public interface IInboundEmailParser
{
    InboundParsedEmail Parse(Stream mimeStream);
}

public sealed record InboundParsedEmail : IDisposable
{
    public string FromEmail { get; init; } = string.Empty;
    public string? FromName { get; init; }
    public IReadOnlyList<EmailAddress> ToAddresses { get; init; } = Array.Empty<EmailAddress>();
    public IReadOnlyList<EmailAddress> CcAddresses { get; init; } = Array.Empty<EmailAddress>();
    public IReadOnlyList<EmailAddress> BccAddresses { get; init; } = Array.Empty<EmailAddress>();
    public string? ReplyTo { get; init; }
    public string? Subject { get; init; }
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public string? InReplyTo { get; init; }
    public string? References { get; init; }
    public IReadOnlyList<ParsedAttachment> Attachments { get; init; } = Array.Empty<ParsedAttachment>();

    public void Dispose()
    {
        foreach (var attachment in Attachments)
        {
            attachment.Content?.Dispose();
        }
    }
}

public sealed record EmailAddress(string Email, string? Name);

public sealed record ParsedAttachment
{
    public string Filename { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
    public string? ContentId { get; init; }
    public bool IsInline { get; init; }
}
