using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public static class GetPlatformSummaryEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/summary", async (IMediator mediator) =>
        {
            var query = new GetPlatformSummaryQuery();
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetPlatformSummary")
        .WithSummary("Get platform summary")
        .WithDescription("Returns platform-wide summary statistics.")
        .Produces<ApiResponse<PlatformSummaryResult>>(StatusCodes.Status200OK);
    }
}
