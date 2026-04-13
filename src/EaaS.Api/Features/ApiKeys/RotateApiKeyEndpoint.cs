using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.ApiKeys;

public static class RotateApiKeyEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/rotate", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new RotateApiKeyCommand(id, tenantId);
            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new
            {
                key_id = result.KeyId,
                api_key = result.ApiKey,
                prefix = result.Prefix,
                old_key_expires_at = result.OldKeyExpiresAt,
                created_at = result.CreatedAt
            }));
        })
        .WithName("RotateApiKey")
        .WithSummary("Rotate an API key")
        .WithDescription("Creates a new API key and puts the old one in a 24-hour grace period.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
