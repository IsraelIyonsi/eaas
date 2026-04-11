using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public static class GetPlatformTimelineEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/timeline", async (IMediator mediator) =>
        {
            var query = new GetPlatformTimelineQuery();
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetPlatformTimeline")
        .WithSummary("Get platform email timeline")
        .WithDescription("Returns daily email volume for the last 30 days.")
        .Produces<ApiResponse<IReadOnlyList<TimelineDataPoint>>>(StatusCodes.Status200OK);
    }
}
