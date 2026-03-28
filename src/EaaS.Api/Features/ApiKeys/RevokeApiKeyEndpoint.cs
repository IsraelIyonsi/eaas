using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public static class RevokeApiKeyEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new RevokeApiKeyCommand(id, tenantId);
            await mediator.Send(command);

            return Results.Ok(new ApiResponse<object?>(true, null));
        })
        .WithName("RevokeApiKey")
        .WithOpenApi()
        .Produces<ApiResponse<object?>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
