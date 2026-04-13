using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public static class GetGrowthMetricsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/growth", async (IMediator mediator) =>
        {
            var query = new GetGrowthMetricsQuery();
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetGrowthMetrics")
        .WithSummary("Get growth metrics")
        .WithDescription("Returns new tenant count per month for the last 12 months.")
        .Produces<ApiResponse<GrowthMetricResult>>(StatusCodes.Status200OK);
    }
}
