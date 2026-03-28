using MediatR;

namespace EaaS.Api.Features.Suppressions;

public sealed record RemoveSuppressionCommand(Guid Id, Guid TenantId) : IRequest;
