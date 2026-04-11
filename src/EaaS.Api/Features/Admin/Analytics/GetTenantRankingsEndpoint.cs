using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public static class GetTenantRankingsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/tenants/rankings", async (IMediator mediator) =>
        {
            var query = new GetTenantRankingsQuery();
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetTenantRankings")
        .WithSummary("Get tenant rankings")
        .WithDescription("Returns top 10 tenants ranked by email volume.")
        .Produces<ApiResponse<IReadOnlyList<TenantRankingResult>>>(StatusCodes.Status200OK);
    }
}
