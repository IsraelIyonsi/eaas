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
            .Select(t => new TenantRankingResult(
                t.Id,
                t.Name,
                t.Emails.Count))
            .OrderByDescending(r => r.EmailCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        return rankings;
    }
}
