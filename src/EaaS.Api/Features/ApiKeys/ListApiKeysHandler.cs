using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.ApiKeys;

public sealed class ListApiKeysHandler : IRequestHandler<ListApiKeysQuery, IReadOnlyList<ApiKeySummary>>
{
    private readonly AppDbContext _dbContext;

    public ListApiKeysHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ApiKeySummary>> Handle(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.TenantId == request.TenantId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeySummary(
                k.Id,
                k.Name,
                k.Prefix,
                k.Status == ApiKeyStatus.Active,
                k.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
