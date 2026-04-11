using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Analytics;

public static class InboundAnalyticsEndpoints
{
    public static void MapInboundAnalytics(RouteGroupBuilder group)
    {
        group.MapGet("/inbound/summary", async (
            HttpContext httpContext,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var tenantId = Guid.Parse(
                httpContext.User.FindFirst("TenantId")?.Value ?? Guid.Empty.ToString());

            var totalReceived = await dbContext.InboundEmails
                .Where(e => e.TenantId == tenantId)
                .CountAsync(cancellationToken);

            var processed = await dbContext.InboundEmails
                .Where(e => e.TenantId == tenantId && e.Status == InboundEmailStatus.Processed)
                .CountAsync(cancellationToken);

            var failed = await dbContext.InboundEmails
                .Where(e => e.TenantId == tenantId && e.Status == InboundEmailStatus.Failed)
                .CountAsync(cancellationToken);

            var forwarded = await dbContext.InboundEmails
                .Where(e => e.TenantId == tenantId && e.Status == InboundEmailStatus.Forwarded)
                .CountAsync(cancellationToken);

            var topSenders = await dbContext.InboundEmails
                .Where(e => e.TenantId == tenantId)
                .GroupBy(e => e.FromEmail)
                .Select(g => new { email = g.Key, count = g.Count(), lastReceivedAt = g.Max(e => e.ReceivedAt) })
                .OrderByDescending(g => g.count)
                .Take(10)
                .ToListAsync(cancellationToken);

            return Results.Ok(ApiResponse.Ok(new
            {
                totalReceived,
                processed,
                failed,
                forwarded,
                avgProcessingTimeMs = 0,
                topSenders,
            }));
        })
        .WithName("GetInboundAnalyticsSummary");

        group.MapGet("/inbound/timeline", async (
            HttpContext httpContext,
            AppDbContext dbContext,
            string granularity = "day",
            CancellationToken cancellationToken = default) =>
        {
            var tenantId = Guid.Parse(
                httpContext.User.FindFirst("TenantId")?.Value ?? Guid.Empty.ToString());

            var since = granularity == "hour"
                ? DateTime.UtcNow.AddHours(-24)
                : DateTime.UtcNow.AddDays(-30);

            var emails = await dbContext.InboundEmails
                .Where(e => e.TenantId == tenantId && e.ReceivedAt >= since)
                .Select(e => new { e.ReceivedAt, e.Status })
                .ToListAsync(cancellationToken);

            var points = emails
                .GroupBy(e => granularity == "hour"
                    ? e.ReceivedAt.ToString("yyyy-MM-ddTHH:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
                    : e.ReceivedAt.ToString("yyyy-MM-ddT00:00:00Z", System.Globalization.CultureInfo.InvariantCulture))
                .Select(g => new
                {
                    timestamp = g.Key,
                    received = g.Count(),
                    processed = g.Count(e => e.Status == InboundEmailStatus.Processed),
                    failed = g.Count(e => e.Status == InboundEmailStatus.Failed),
                    forwarded = g.Count(e => e.Status == InboundEmailStatus.Forwarded),
                })
                .OrderBy(p => p.timestamp)
                .ToList();

            return Results.Ok(ApiResponse.Ok(new
            {
                granularity,
                points,
            }));
        })
        .WithName("GetInboundAnalyticsTimeline");
    }
}
