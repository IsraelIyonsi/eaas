using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Health;

public static class GetSystemHealthEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMediator mediator) =>
        {
            var query = new GetSystemHealthQuery();
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetSystemHealth")
        .WithSummary("Get system health")
        .WithDescription("Returns health status of all system services.")
        .Produces<ApiResponse<SystemHealthResult>>(StatusCodes.Status200OK);
    }
}
