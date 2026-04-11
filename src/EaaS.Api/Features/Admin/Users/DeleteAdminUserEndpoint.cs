using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Users;

public static class DeleteAdminUserEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);
            var command = new DeleteAdminUserCommand(adminUserId, id);
            await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new { message = "Admin user deactivated" }));
        })
        .WithName("DeleteAdminUser")
        .WithSummary("Delete an admin user")
        .WithDescription("Deactivates an admin user account.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
