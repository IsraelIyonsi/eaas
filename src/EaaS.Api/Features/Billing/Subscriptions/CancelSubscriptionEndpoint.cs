using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public static class CancelSubscriptionEndpoint
{
    public sealed record CancelSubscriptionRequest(bool Immediate = false);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/cancel", async (CancelSubscriptionRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            var command = new CancelSubscriptionCommand(tenantId, request.Immediate);
            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("CancelSubscription")
        .WithSummary("Cancel subscription")
        .WithDescription("Cancels the current subscription. By default, cancels at end of billing period. Set immediate=true to cancel immediately.")
        .Produces<ApiResponse<SubscriptionResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
