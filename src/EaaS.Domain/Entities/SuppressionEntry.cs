using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class SuppressionEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public SuppressionReason Reason { get; set; }
    public string? SourceMessageId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SuppressedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
}
