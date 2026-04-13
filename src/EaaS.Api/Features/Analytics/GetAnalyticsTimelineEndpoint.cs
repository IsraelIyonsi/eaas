using EaaS.Shared.Constants;
using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Analytics;

public static class GetAnalyticsTimelineEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/timeline", async (
            HttpContext httpContext,
            IMediator mediator,
            DateTime? date_from,
            DateTime? date_to,
            string? granularity,
            string? domain,
            Guid? api_key_id,
            Guid? template_id) =>
        {
            var tenantId = GetTenantId(httpContext);
            var from = date_from ?? DateTime.UtcNow.AddDays(-AnalyticsConstants.DefaultDateRangeDays);
            var to = date_to ?? DateTime.UtcNow;
            var interval = granularity ?? "day";

            var query = new GetAnalyticsTimelineQuery(tenantId, from, to, interval, domain, api_key_id, template_id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(new
            {
                granularity = result.Granularity,
                points = result.Points.Select(p => new
                {
                    timestamp = p.Timestamp,
                    sent = p.Sent,
                    delivered = p.Delivered,
                    bounced = p.Bounced,
                    complained = p.Complained,
                    opened = p.Opened,
                    clicked = p.Clicked
                })
            }));
        })
        .WithName("GetAnalyticsTimeline")
        .WithSummary("Get email analytics timeline")
        .WithDescription("Returns time-series email metrics grouped by hour or day.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
