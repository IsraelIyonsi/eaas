using EaaS.Shared.Contracts;

namespace EaaS.Api.Features.CustomerAuth;

public static class CustomerLogoutEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/logout", () =>
        {
            return Results.Ok(ApiResponse.Ok(new { Message = "Logged out successfully." }));
        })
        .WithName("CustomerLogout")
        .WithSummary("Log out a customer session")
        .WithDescription("Returns 200. Cookie clearing is handled by the dashboard Next.js route.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK);
    }
}
