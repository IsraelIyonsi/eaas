using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class InboundRule
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DomainId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MatchPattern { get; set; } = string.Empty;
    public InboundRuleAction Action { get; set; } = InboundRuleAction.Store;
    public string? WebhookUrl { get; set; }
    public string? ForwardTo { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public SendingDomain Domain { get; set; } = null!;
}
