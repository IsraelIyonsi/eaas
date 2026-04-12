using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public static class ListBillingPlansEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMediator mediator) =>
        {
            var query = new ListBillingPlansQuery();
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListBillingPlans")
        .WithSummary("List available billing plans")
        .WithDescription("Returns all active billing plans available for subscription, sorted by price.")
        .Produces<ApiResponse<List<BillingPlanResult>>>(StatusCodes.Status200OK);
    }
}
