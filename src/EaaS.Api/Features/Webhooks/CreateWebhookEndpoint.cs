using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Webhooks;

public static class CreateWebhookEndpoint
{
    public sealed record CreateWebhookRequest(
        string Url,
        string[] Events,
        string? Secret);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateWebhookRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new CreateWebhookCommand(tenantId, request.Url, request.Events, request.Secret);
            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/webhooks/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("CreateWebhook")
        .WithSummary("Create a webhook endpoint")
        .WithDescription("Registers a new webhook URL to receive email event notifications.")
        .Produces<ApiResponse<WebhookCreatedDto>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
