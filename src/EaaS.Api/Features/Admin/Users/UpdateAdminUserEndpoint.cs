using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Users;

public static class UpdateAdminUserEndpoint
{
    public sealed record UpdateAdminUserRequest(
        string? Email,
        string? DisplayName,
        string? Password,
        string? Role,
        bool? IsActive);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateAdminUserRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);

            var command = new UpdateAdminUserCommand(
                adminUserId,
                id,
                request.Email,
                request.DisplayName,
                request.Password,
                request.Role,
                request.IsActive);

            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("UpdateAdminUser")
        .WithSummary("Update an admin user")
        .WithDescription("Updates admin user information.")
        .Produces<ApiResponse<AdminUserResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
