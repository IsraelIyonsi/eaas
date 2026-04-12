using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed class GetTenantRankingsHandler : IRequestHandler<GetTenantRankingsQuery, IReadOnlyList<TenantRankingResult>>
{
    private readonly AppDbContext _dbContext;

    public GetTenantRankingsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TenantRankingResult>> Handle(GetTenantRankingsQuery request, CancellationToken cancellationToken)
    {
        var rankings = await _dbContext.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.Emails.Count)
            .Take(10)
            .Select(t => new TenantRankingResult(
                t.Id,
                t.Name,
                t.Emails.Count))
            .ToListAsync(cancellationToken);

        return rankings;
    }
}
