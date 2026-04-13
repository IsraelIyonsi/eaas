using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Webhooks;

public static class UpdateWebhookEndpoint
{
    public sealed record UpdateWebhookRequest(
        string? Url,
        string[]? Events,
        string? Status);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateWebhookRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new UpdateWebhookCommand(id, tenantId, request.Url, request.Events, request.Status);
            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("UpdateWebhook")
        .WithSummary("Update a webhook endpoint")
        .WithDescription("Updates an existing webhook configuration. Partial updates are supported.")
        .Produces<ApiResponse<WebhookDto>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
