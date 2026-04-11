using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public static class GetSubscriptionEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/current", async (HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetSubscriptionQuery(tenantId);
            var result = await mediator.Send(query);

            if (result is null)
                return Results.NotFound(ApiErrorResponse.Create("NOT_FOUND", "No active subscription found."));

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetCurrentSubscription")
        .WithSummary("Get current subscription")
        .WithDescription("Returns the current active subscription for the authenticated tenant.")
        .Produces<ApiResponse<SubscriptionResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
