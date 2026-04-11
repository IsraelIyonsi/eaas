using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public static class ListTenantsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            IMediator mediator,
            int page = 1,
            int pageSize = 20,
            string? status = null,
            string? search = null) =>
        {
            var query = new ListTenantsQuery(page, pageSize, status, search);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListTenants")
        .WithSummary("List all tenants")
        .WithDescription("Returns paginated list of all tenants with summary counts.")
        .Produces<ApiResponse<PagedResponse<TenantSummaryResult>>>(StatusCodes.Status200OK);
    }
}
