using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public sealed record ListBillingPlansQuery() : IRequest<List<BillingPlanResult>>;

public sealed record BillingPlanResult(
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
    bool PrioritySupport);
