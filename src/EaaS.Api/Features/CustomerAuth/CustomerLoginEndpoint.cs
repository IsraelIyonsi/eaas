using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.CustomerAuth;

public static class CustomerLoginEndpoint
{
    public sealed record CustomerLoginRequest(string Email, string Password);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", async (CustomerLoginRequest request, IMediator mediator) =>
        {
            var command = new CustomerLoginCommand(request.Email, request.Password);
            var result = await mediator.Send(command);
            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("CustomerLogin")
        .WithSummary("Authenticate a customer account")
        .WithDescription("Validates customer credentials and returns tenant information for session creation.")
        .Produces<ApiResponse<CustomerLoginResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }
}
