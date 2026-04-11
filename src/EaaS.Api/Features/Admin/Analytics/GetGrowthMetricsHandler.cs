using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed class GetGrowthMetricsHandler : IRequestHandler<GetGrowthMetricsQuery, IReadOnlyList<GrowthMetricResult>>
{
    private readonly AppDbContext _dbContext;

    public GetGrowthMetricsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GrowthMetricResult>> Handle(GetGrowthMetricsQuery request, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddMonths(-12);

        var metrics = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.CreatedAt >= since)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new GrowthMetricResult(
                g.Key.Year,
                g.Key.Month,
                g.Count()))
            .OrderBy(m => m.Year)
            .ThenBy(m => m.Month)
            .ToListAsync(cancellationToken);

        return metrics;
    }
}
