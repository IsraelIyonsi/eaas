using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public sealed record ListApiKeysQuery(Guid TenantId) : IRequest<IReadOnlyList<ApiKeySummary>>;

public sealed record ApiKeySummary(
    Guid Id,
    string Name,
    string KeyPrefix,
    bool IsActive,
    DateTime CreatedAt);
