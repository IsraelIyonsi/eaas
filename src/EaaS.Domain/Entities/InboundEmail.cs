using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class InboundEmail
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string ToEmails { get; set; } = "[]";
    public string CcEmails { get; set; } = "[]";
    public string BccEmails { get; set; } = "[]";
    public string? ReplyTo { get; set; }
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public string? Headers { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Metadata { get; set; } = "{}";
    public InboundEmailStatus Status { get; set; } = InboundEmailStatus.Received;
    public string? S3Key { get; set; }
    public decimal? SpamScore { get; set; }
    public string? SpamVerdict { get; set; }
    public string? VirusVerdict { get; set; }
    public string? SpfVerdict { get; set; }
    public string? DkimVerdict { get; set; }
    public string? DmarcVerdict { get; set; }
    public string? InReplyTo { get; set; }
    public string? References { get; set; }
    public Guid? OutboundEmailId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Email? OutboundEmail { get; set; }
    public ICollection<InboundAttachment> Attachments { get; set; } = new List<InboundAttachment>();
}
