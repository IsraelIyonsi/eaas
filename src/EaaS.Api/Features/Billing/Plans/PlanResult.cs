namespace EaaS.Api.Features.Billing.Plans;

public sealed record PlanResult(
    Guid Id,
    string Name,
    string Tier,
    decimal MonthlyPriceUsd,
    decimal AnnualPriceUsd,
    int DailyEmailLimit,
    long MonthlyEmailLimit,
    int MaxApiKeys,
    int MaxDomains,
    int MaxTemplates,
    int MaxWebhooks,
    bool CustomDomainBranding,
    bool PrioritySupport,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
