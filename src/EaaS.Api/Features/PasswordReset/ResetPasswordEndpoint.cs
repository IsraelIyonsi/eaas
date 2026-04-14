using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.PasswordReset;

public static class ResetPasswordEndpoint
{
    public sealed record ResetPasswordRequest(string Token, string NewPassword);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/reset-password", async (ResetPasswordRequest request, IMediator mediator) =>
        {
            var command = new ResetPasswordCommand(request.Token ?? string.Empty, request.NewPassword ?? string.Empty);
            var result = await mediator.Send(command);
            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ResetPassword")
        .WithSummary("Complete a password reset using a one-time token")
        .WithDescription("Consumes the reset token, hashes the new password with BCrypt, and updates the tenant record.")
        .Produces<ApiResponse<ResetPasswordResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }
}
