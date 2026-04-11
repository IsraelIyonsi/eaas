namespace EaaS.Api.Features.Admin.Tenants;

public sealed record TenantSummaryResult(
    Guid Id,
    string Name,
    string Status,
    string? CompanyName,
    string? ContactEmail,
    int ApiKeyCount,
    int DomainCount,
    int EmailCount,
    DateTime CreatedAt);
