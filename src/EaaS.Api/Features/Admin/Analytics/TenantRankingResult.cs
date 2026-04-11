namespace EaaS.Api.Features.Admin.Analytics;

public sealed record TenantRankingResult(
    Guid TenantId,
    string TenantName,
    int EmailCount);
