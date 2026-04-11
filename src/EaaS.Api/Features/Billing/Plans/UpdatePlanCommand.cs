using EaaS.Domain.Enums;
using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public sealed record UpdatePlanCommand(
    Guid PlanId,
    string? Name,
    PlanTier? Tier,
    decimal? MonthlyPriceUsd,
    decimal? AnnualPriceUsd,
    int? DailyEmailLimit,
    long? MonthlyEmailLimit,
    int? MaxApiKeys,
    int? MaxDomains,
    int? MaxTemplates,
    int? MaxWebhooks,
    bool? CustomDomainBranding,
    bool? PrioritySupport,
    bool? IsActive) : IRequest<PlanResult>;
