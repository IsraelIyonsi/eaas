using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed record GetTenantQuery(Guid TenantId) : IRequest<TenantDetailResult>;
