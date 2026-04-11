namespace EaaS.Api.Features.Admin.Analytics;

public sealed record PlatformSummaryResult(
    int TotalTenants,
    int ActiveTenants,
    int TotalEmails,
    int TotalDomains,
    int TotalApiKeys);
