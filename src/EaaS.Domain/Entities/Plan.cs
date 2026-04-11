using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlanTier Tier { get; set; } = PlanTier.Free;
    public decimal MonthlyPriceUsd { get; set; }
    public decimal AnnualPriceUsd { get; set; }
    public int DailyEmailLimit { get; set; }
    public long MonthlyEmailLimit { get; set; }
    public int MaxApiKeys { get; set; }
    public int MaxDomains { get; set; }
    public int MaxTemplates { get; set; }
    public int MaxWebhooks { get; set; }
    public bool CustomDomainBranding { get; set; }
    public bool PrioritySupport { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
