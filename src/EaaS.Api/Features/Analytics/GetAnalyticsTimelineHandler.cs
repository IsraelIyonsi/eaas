using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Analytics;

public sealed class GetAnalyticsTimelineHandler : IRequestHandler<GetAnalyticsTimelineQuery, AnalyticsTimelineResult>
{
    private readonly AppDbContext _dbContext;

    public GetAnalyticsTimelineHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AnalyticsTimelineResult> Handle(GetAnalyticsTimelineQuery request, CancellationToken cancellationToken)
    {
        var granularity = string.Equals(request.Granularity, "hour", StringComparison.OrdinalIgnoreCase)
            ? "hour"
            : "day";

        var maxRange = granularity == "hour" ? AnalyticsConstants.HourlyMaxRange : AnalyticsConstants.DailyMaxRange;
        var dateFrom = request.DateFrom;
        var dateTo = request.DateTo;

        if (dateTo - dateFrom > maxRange)
            dateFrom = dateTo - maxRange;

        // Build raw SQL for DATE_TRUNC - use parameterized query for safety
        var parameters = new List<object> { request.TenantId, dateFrom, dateTo };
        var paramIndex = 3;

        var sql = "SELECT " +
            "DATE_TRUNC('" + granularity + "', e.created_at) AS \"Timestamp\", " +
            "COUNT(*)::int AS \"Sent\", " +
            "COUNT(*) FILTER (WHERE e.status = 'delivered')::int AS \"Delivered\", " +
            "COUNT(*) FILTER (WHERE e.status = 'bounced')::int AS \"Bounced\", " +
            "COUNT(*) FILTER (WHERE e.status = 'complained')::int AS \"Complained\", " +
            "COUNT(*) FILTER (WHERE e.opened_at IS NOT NULL)::int AS \"Opened\", " +
            "COUNT(*) FILTER (WHERE e.clicked_at IS NOT NULL)::int AS \"Clicked\" " +
            "FROM emails e " +
            "WHERE e.tenant_id = {0} " +
            "AND e.created_at >= {1} " +
            "AND e.created_at <= {2}";

        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            sql += " AND e.from_email ILIKE {" + paramIndex + "}";
            parameters.Add("%" + "@" + request.Domain);
            paramIndex++;
        }

        if (request.ApiKeyId.HasValue)
        {
            sql += " AND e.api_key_id = {" + paramIndex + "}";
            parameters.Add(request.ApiKeyId.Value);
            paramIndex++;
        }

        if (request.TemplateId.HasValue)
        {
            sql += " AND e.template_id = {" + paramIndex + "}";
            parameters.Add(request.TemplateId.Value);
            paramIndex++;
        }

        sql += " GROUP BY DATE_TRUNC('" + granularity + "', e.created_at)" +
               " ORDER BY \"Timestamp\" ASC";

        var points = await _dbContext.Database
            .SqlQueryRaw<TimelinePoint>(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);

        return new AnalyticsTimelineResult(granularity, points);
    }
}
