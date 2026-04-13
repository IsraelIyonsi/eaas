using EaaS.Shared.Contracts;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Inbound.Emails;

public static class RetryInboundWebhookEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/retry-webhook", async (
            Guid id,
            HttpContext httpContext,
            RetryInboundWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var tenantId = GetTenantId(httpContext);
            await handler.HandleAsync(id, tenantId, cancellationToken);

            return Results.Ok(ApiResponse.Ok(new
            {
                message = "Webhook retry dispatched successfully."
            }));
        })
        .WithName("RetryInboundWebhook")
        .WithSummary("Retry webhook dispatch for an inbound email")
        .WithDescription("Re-dispatches the inbound email event through the webhook system.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
