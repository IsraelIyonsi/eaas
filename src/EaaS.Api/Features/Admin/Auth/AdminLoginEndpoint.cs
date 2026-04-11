using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Auth;

public static class AdminLoginEndpoint
{
    public sealed record AdminLoginRequest(string Email, string Password);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", async (
            AdminLoginRequest request,
            HttpContext httpContext,
            IMediator mediator) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var command = new AdminLoginCommand(request.Email, request.Password, ipAddress);
            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("AdminLogin")
        .WithSummary("Authenticate an admin user")
        .WithDescription("Validates admin credentials and returns user information for session creation.")
        .Produces<ApiResponse<AdminLoginResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }
}
