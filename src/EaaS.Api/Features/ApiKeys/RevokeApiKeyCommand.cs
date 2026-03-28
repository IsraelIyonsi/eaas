using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public sealed record RevokeApiKeyCommand(Guid Id, Guid TenantId) : IRequest;
