using EaaS.Api.Constants;
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
                httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value ?? Guid.Empty.ToString());

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

            var processingRate = totalReceived > 0
                ? (double)processed / totalReceived
                : 0.0;

            return Results.Ok(ApiResponse.Ok(new
            {
                total_received = totalReceived,
                processed,
                failed,
                forwarded,
                spam_flagged = 0,
                virus_flagged = 0,
                avg_processing_time_ms = 0,
                processing_rate = processingRate,
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
                httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value ?? Guid.Empty.ToString());

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

        group.MapGet("/inbound/top-senders", async (
            HttpContext httpContext,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            string? date_from = null,
            string? date_to = null,
            int limit = PaginationConstants.DefaultTopLimit,
            CancellationToken cancellationToken = default) =>
        {
            var tenantId = Guid.Parse(
                httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value ?? Guid.Empty.ToString());

            try
            {
                var query = dbContext.InboundEmails
                    .Where(e => e.TenantId == tenantId);

                if (DateTime.TryParse(date_from, out var from))
                    query = query.Where(e => e.ReceivedAt >= from);

                if (DateTime.TryParse(date_to, out var to))
                    query = query.Where(e => e.ReceivedAt <= to);

                var senders = await query
                    .GroupBy(e => e.FromEmail)
                    .Select(g => new
                    {
                        email = g.Key,
                        total_emails = g.Count(),
                        last_received_at = g.Max(e => e.ReceivedAt)
                    })
                    .OrderByDescending(s => s.total_emails)
                    .Take(limit)
                    .ToListAsync(cancellationToken);

                return Results.Ok(ApiResponse.Ok(senders));
            }
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger("InboundAnalytics");
#pragma warning disable CA1848
                logger.LogError(ex, "Failed to query inbound top senders for tenant {TenantId}", tenantId);
#pragma warning restore CA1848
                return Results.Ok(ApiResponse.Ok(Array.Empty<object>()));
            }
        })
        .WithName("GetInboundTopSenders");
    }
}
