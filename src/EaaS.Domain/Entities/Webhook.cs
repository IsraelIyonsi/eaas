using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class Webhook
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string[] Events { get; set; } = Array.Empty<string>();
    public string? Secret { get; set; }
    public WebhookStatus Status { get; set; } = WebhookStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
}
