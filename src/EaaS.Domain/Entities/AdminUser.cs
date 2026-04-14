using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

/// <summary>
/// Platform-level entity. Not multi-tenant.
/// AdminUser represents a system administrator who manages tenants and platform settings.
/// Intentionally has no TenantId — admin users operate across all tenants.
/// </summary>
public class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    public AdminRole Role { get; set; } = AdminRole.Admin;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
