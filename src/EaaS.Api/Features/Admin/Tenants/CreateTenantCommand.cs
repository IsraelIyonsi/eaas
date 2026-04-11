using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed record CreateTenantCommand(
    Guid AdminUserId,
    string Name,
    string? ContactEmail,
    string? CompanyName,
    int? MaxApiKeys,
    int? MaxDomainsCount,
    long? MonthlyEmailLimit,
    string? Notes) : IRequest<TenantDetailResult>;
