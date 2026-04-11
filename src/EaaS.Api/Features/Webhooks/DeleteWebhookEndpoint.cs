using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Webhooks;

public static class DeleteWebhookEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new DeleteWebhookCommand(id, tenantId);
            await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new
            {
                message = "Webhook deleted"
            }));
        })
        .WithName("DeleteWebhook")
        .WithSummary("Delete a webhook endpoint")
        .WithDescription("Removes a webhook configuration permanently.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
