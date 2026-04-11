namespace EaaS.Api.Features.Admin.Tenants;

public sealed record TenantDetailResult(
    Guid Id,
    string Name,
    string Status,
    string? CompanyName,
    string? ContactEmail,
    int? MaxApiKeys,
    int? MaxDomainsCount,
    long? MonthlyEmailLimit,
    string? Notes,
    int ApiKeyCount,
    int DomainCount,
    int EmailCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
