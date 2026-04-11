namespace EaaS.Domain.Entities;

public class InboundAttachment
{
    public Guid Id { get; set; }
    public Guid InboundEmailId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string? ContentId { get; set; }
    public bool IsInline { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public InboundEmail InboundEmail { get; set; } = null!;
}
