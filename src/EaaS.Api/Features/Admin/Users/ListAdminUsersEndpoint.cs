using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Users;

public static class ListAdminUsersEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            IMediator mediator,
            int page = 1,
            int pageSize = 20) =>
        {
            var query = new ListAdminUsersQuery(page, pageSize);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListAdminUsers")
        .WithSummary("List admin users")
        .WithDescription("Returns paginated list of all admin users.")
        .Produces<ApiResponse<PagedResponse<AdminUserResult>>>(StatusCodes.Status200OK);
    }
}
