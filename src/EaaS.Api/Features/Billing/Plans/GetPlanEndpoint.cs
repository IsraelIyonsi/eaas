using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public static class GetPlanEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var query = new GetPlanQuery(id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetPlan")
        .WithSummary("Get plan details")
        .WithDescription("Returns detailed information about a specific billing plan.")
        .Produces<ApiResponse<PlanResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
