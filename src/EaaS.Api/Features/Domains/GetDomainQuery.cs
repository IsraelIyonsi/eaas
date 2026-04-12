using MediatR;

namespace EaaS.Api.Features.Domains;

public sealed record GetDomainQuery(Guid Id, Guid TenantId) : IRequest<DomainSummary>;
