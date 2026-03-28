using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public sealed record RotateApiKeyCommand(Guid Id, Guid TenantId) : IRequest<RotateApiKeyResult>;

public sealed record RotateApiKeyResult(
    Guid KeyId,
    string ApiKey,
    string Prefix,
    DateTime OldKeyExpiresAt,
    DateTime CreatedAt);
