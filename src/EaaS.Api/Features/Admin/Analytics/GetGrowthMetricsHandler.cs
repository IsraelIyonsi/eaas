using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed class GetGrowthMetricsHandler : IRequestHandler<GetGrowthMetricsQuery, GrowthMetricResult>
{
    private readonly AppDbContext _dbContext;

    public GetGrowthMetricsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GrowthMetricResult> Handle(GetGrowthMetricsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // Tenant growth
        var newTenantsThisMonth = await _dbContext.Tenants
            .CountAsync(t => t.CreatedAt >= thisMonthStart, cancellationToken);

        var newTenantsLastMonth = await _dbContext.Tenants
            .CountAsync(t => t.CreatedAt >= lastMonthStart && t.CreatedAt < thisMonthStart, cancellationToken);

        var tenantGrowthPercent = newTenantsLastMonth > 0
            ? Math.Round((double)(newTenantsThisMonth - newTenantsLastMonth) / newTenantsLastMonth * 100, 1)
            : newTenantsThisMonth > 0 ? 100.0 : 0.0;

        // Email growth
        var emailsThisMonth = await _dbContext.Emails
            .CountAsync(e => e.CreatedAt >= thisMonthStart, cancellationToken);

        var emailsLastMonth = await _dbContext.Emails
            .CountAsync(e => e.CreatedAt >= lastMonthStart && e.CreatedAt < thisMonthStart, cancellationToken);

        var emailGrowthPercent = emailsLastMonth > 0
            ? Math.Round((double)(emailsThisMonth - emailsLastMonth) / emailsLastMonth * 100, 1)
            : emailsThisMonth > 0 ? 100.0 : 0.0;

        return new GrowthMetricResult(
            newTenantsThisMonth,
            newTenantsLastMonth,
            tenantGrowthPercent,
            emailGrowthPercent);
    }
}
