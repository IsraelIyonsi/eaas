using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.PasswordReset;

public static class ForgotPasswordEndpoint
{
    public sealed record ForgotPasswordRequest(string Email);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/forgot-password", async (
                ForgotPasswordRequest request,
                HttpContext httpContext,
                IMediator mediator) =>
            {
                var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString();

                var command = new ForgotPasswordCommand(request.Email ?? string.Empty, ip);
                await mediator.Send(command);

                // Always identical response — no enumeration.
                return Results.Ok(ApiResponse.Ok(new
                {
                    Message = "If an account exists for that email, a reset link has been sent."
                }));
            })
            .WithName("ForgotPassword")
            .WithSummary("Request a password reset email")
            .WithDescription("Always returns 200 to prevent email enumeration. Rate-limited per email and per IP.")
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }
}
