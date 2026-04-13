using EaaS.Shared.Constants;
using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Analytics;

public static class GetAnalyticsSummaryEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/summary", async (
            HttpContext httpContext,
            IMediator mediator,
            DateTime? date_from,
            DateTime? date_to,
            string? domain,
            Guid? api_key_id,
            Guid? template_id) =>
        {
            var tenantId = GetTenantId(httpContext);
            var from = date_from ?? DateTime.UtcNow.AddDays(-AnalyticsConstants.DefaultDateRangeDays);
            var to = date_to ?? DateTime.UtcNow;

            var query = new GetAnalyticsSummaryQuery(tenantId, from, to, domain, api_key_id, template_id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(new
            {
                total_sent = result.TotalSent,
                delivered = result.Delivered,
                bounced = result.Bounced,
                complained = result.Complained,
                opened = result.Opened,
                clicked = result.Clicked,
                failed = result.Failed,
                delivery_rate = result.DeliveryRate,
                open_rate = result.OpenRate,
                click_rate = result.ClickRate,
                bounce_rate = result.BounceRate,
                complaint_rate = result.ComplaintRate
            }));
        })
        .WithName("GetAnalyticsSummary")
        .WithSummary("Get email analytics summary")
        .WithDescription("Returns aggregated email metrics for the specified date range.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
