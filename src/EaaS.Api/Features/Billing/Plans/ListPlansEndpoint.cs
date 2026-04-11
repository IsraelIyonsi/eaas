using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public static class ListPlansEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMediator mediator) =>
        {
            var query = new ListPlansQuery();
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListPlans")
        .WithSummary("List all active billing plans")
        .WithDescription("Returns all active billing plans sorted by price.")
        .Produces<ApiResponse<List<PlanResult>>>(StatusCodes.Status200OK);
    }
}
