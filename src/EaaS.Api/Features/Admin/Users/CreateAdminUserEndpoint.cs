using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Users;

public static class CreateAdminUserEndpoint
{
    public sealed record CreateAdminUserRequest(
        string Email,
        string DisplayName,
        string Password,
        string Role);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateAdminUserRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);

            var command = new CreateAdminUserCommand(
                adminUserId,
                request.Email,
                request.DisplayName,
                request.Password,
                request.Role);

            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/admin/users/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("CreateAdminUser")
        .WithSummary("Create an admin user")
        .WithDescription("Creates a new admin user with the specified role.")
        .Produces<ApiResponse<AdminUserResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }
}
