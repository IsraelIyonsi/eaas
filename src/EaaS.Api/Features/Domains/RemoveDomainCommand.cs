using MediatR;

namespace EaaS.Api.Features.Domains;

public sealed record RemoveDomainCommand(Guid Id, Guid TenantId) : IRequest;
