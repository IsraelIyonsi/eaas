using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public static class CreateSubscriptionEndpoint
{
    public sealed record CreateSubscriptionRequest(Guid PlanId, string? Provider);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateSubscriptionRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            var command = new CreateSubscriptionCommand(tenantId, request.PlanId, request.Provider);
            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/billing/subscriptions/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("CreateSubscription")
        .WithSummary("Create a new subscription")
        .WithDescription("Creates a new subscription for the authenticated tenant. Free plans are activated immediately; paid plans start with a 14-day trial.")
        .Produces<ApiResponse<SubscriptionResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
