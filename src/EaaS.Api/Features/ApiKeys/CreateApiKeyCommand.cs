using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public sealed record CreateApiKeyCommand(string Name, Guid TenantId) : IRequest<CreateApiKeyResult>;

public sealed record CreateApiKeyResult(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Key,
    DateTime CreatedAt);
