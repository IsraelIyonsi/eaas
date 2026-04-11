using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public static class GetTenantEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var query = new GetTenantQuery(id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetTenant")
        .WithSummary("Get tenant details")
        .WithDescription("Returns detailed information about a specific tenant.")
        .Produces<ApiResponse<TenantDetailResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
