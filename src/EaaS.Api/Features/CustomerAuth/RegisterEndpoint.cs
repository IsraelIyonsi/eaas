using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.CustomerAuth;

public static class RegisterEndpoint
{
    public sealed record RegisterRequest(string Name, string Email, string Password, string? CompanyName);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterRequest request, IMediator mediator) =>
        {
            var command = new RegisterCommand(request.Name, request.Email, request.Password, request.CompanyName);
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/tenants/{result.TenantId}", ApiResponse.Ok(result));
        })
        .WithName("RegisterCustomer")
        .WithSummary("Register a new customer account")
        .Produces<ApiResponse<RegisterResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }
}
