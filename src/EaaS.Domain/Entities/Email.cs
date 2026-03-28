using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class Email
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApiKeyId { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string? BatchId { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string ToEmails { get; set; } = "[]";
    public string CcEmails { get; set; } = "[]";
    public string BccEmails { get; set; } = "[]";
    public string Subject { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public Guid? TemplateId { get; set; }
    public string? Variables { get; set; }
    public string Attachments { get; set; } = "[]";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Metadata { get; set; } = "{}";
    public bool TrackOpens { get; set; } = true;
    public bool TrackClicks { get; set; } = true;
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public string? SesMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClickedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ApiKey ApiKey { get; set; } = null!;
    public ICollection<EmailEvent> Events { get; set; } = new List<EmailEvent>();
}
