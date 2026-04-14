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

    /// <summary>
    /// Running count of consecutive delivery failures. Reset to 0 on any success.
    /// When it reaches <see cref="EaaS.Shared.Constants.WebhookConstants.AutoDisableThreshold"/>
    /// the webhook transitions to <see cref="WebhookStatus.Disabled"/>.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
}
