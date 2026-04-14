namespace SendNex.Mailgun.Dtos;

/// <summary>
/// Mailgun outbound send DTO — neutral shape fed to <see cref="MailgunClient"/>. The
/// client owns the translation to <c>multipart/form-data</c>. This type never crosses
/// the adapter boundary; callers on the Infrastructure side speak the domain's
/// <c>SendEmailRequest</c>, not this DTO.
/// </summary>
public sealed record MailgunSendRequest
{
    public required string Domain { get; init; }
    public required string From { get; init; }
    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string>? Cc { get; init; }
    public IReadOnlyList<string>? Bcc { get; init; }
    public required string Subject { get; init; }
    public string? Text { get; init; }
    public string? Html { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; init; }
    public IReadOnlyDictionary<string, string>? CustomVariables { get; init; }
    public IReadOnlyList<MailgunAttachment>? Attachments { get; init; }
    public bool? TrackingOpens { get; init; }
    public bool? TrackingClicks { get; init; }
}

public sealed record MailgunAttachment(
    string Filename,
    string ContentType,
    Stream Content);
