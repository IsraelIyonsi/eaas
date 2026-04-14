using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

/// <summary>
/// A billing plan offered to tenants, defining pricing, quotas and feature flags.
/// Plans are seeded/managed administratively and are referenced by
/// <see cref="Subscription"/> records for each tenant.
/// </summary>
public class Plan
{
    /// <summary>Primary key for the plan.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable plan name shown on pricing pages and invoices (e.g. "Starter", "Pro").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tier bucket used for feature gating and upgrade ordering.</summary>
    public PlanTier Tier { get; set; } = PlanTier.Free;

    /// <summary>Recurring monthly price billed to subscribers, expressed in USD.</summary>
    public decimal MonthlyPriceUsd { get; set; }

    /// <summary>Recurring annual price billed to subscribers, expressed in USD.</summary>
    public decimal AnnualPriceUsd { get; set; }

    /// <summary>Maximum number of emails a subscriber may send in a rolling 24-hour window.</summary>
    public int DailyEmailLimit { get; set; }

    /// <summary>Maximum number of emails a subscriber may send within a calendar month.</summary>
    public long MonthlyEmailLimit { get; set; }

    /// <summary>Maximum number of active API keys a tenant on this plan may provision.</summary>
    public int MaxApiKeys { get; set; }

    /// <summary>Maximum number of verified sending domains a tenant on this plan may register.</summary>
    public int MaxDomains { get; set; }

    /// <summary>Maximum number of email templates a tenant on this plan may store.</summary>
    public int MaxTemplates { get; set; }

    /// <summary>Maximum number of webhook endpoints a tenant on this plan may register.</summary>
    public int MaxWebhooks { get; set; }

    /// <summary>When true, the tenant may use custom branded tracking/click domains.</summary>
    public bool CustomDomainBranding { get; set; }

    /// <summary>When true, subscribers on this plan qualify for priority support SLAs.</summary>
    public bool PrioritySupport { get; set; }

    /// <summary>When false, the plan is retired and cannot be selected by new subscribers.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the plan record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the plan record was last modified.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Subscriptions currently (or historically) attached to this plan.</summary>
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
