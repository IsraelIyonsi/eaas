using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Webhooks;

public static class TestWebhookEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/test", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new TestWebhookCommand(id, tenantId);
            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                errorMessage = result.ErrorMessage
            }));
        })
        .WithName("TestWebhook")
        .WithSummary("Send a test webhook")
        .WithDescription("Sends a test payload to the webhook URL to verify connectivity.")
        .WithOpenApi()
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
