using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string[] AllowedDomains { get; set; } = Array.Empty<string>();
    public ApiKeyStatus Status { get; set; } = ApiKeyStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Email> Emails { get; set; } = new List<Email>();
}
