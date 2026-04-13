namespace EaaS.Api.Features.Admin.Analytics;

public sealed record GrowthMetricResult(
    int NewTenantsThisMonth,
    int NewTenantsLastMonth,
    double TenantGrowthPercent,
    double EmailGrowthPercent);
