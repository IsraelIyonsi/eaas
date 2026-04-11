using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed record UpdateTenantCommand(
    Guid AdminUserId,
    Guid TenantId,
    string? Name,
    string? ContactEmail,
    string? CompanyName,
    int? MaxApiKeys,
    int? MaxDomainsCount,
    long? MonthlyEmailLimit,
    string? Notes) : IRequest<TenantDetailResult>;
