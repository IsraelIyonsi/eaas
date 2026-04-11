using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public AuditAction Action { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string Details { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public AdminUser AdminUser { get; set; } = null!;
}
