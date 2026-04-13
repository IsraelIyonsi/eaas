using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public static partial class GetGrowthMetricsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/growth", async (IMediator mediator, ILogger<GrowthMetricResult> logger) =>
        {
            try
            {
                var query = new GetGrowthMetricsQuery();
                var result = await mediator.Send(query);

                return Results.Ok(ApiResponse.Ok(result));
            }
            catch (Exception ex)
            {
                LogGrowthMetricsFailure(logger, ex);
                // Return zero-valued fallback so the dashboard degrades gracefully
                var fallback = new GrowthMetricResult(0, 0, 0.0, 0.0);
                return Results.Ok(ApiResponse.Ok(fallback));
            }
        })
        .WithName("GetGrowthMetrics")
        .WithSummary("Get growth metrics")
        .WithDescription("Returns new tenant count per month for the last 12 months.")
        .Produces<ApiResponse<GrowthMetricResult>>(StatusCodes.Status200OK);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to compute growth metrics")]
    private static partial void LogGrowthMetricsFailure(ILogger logger, Exception ex);
}
