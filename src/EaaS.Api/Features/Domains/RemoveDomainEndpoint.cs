using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Domains;

public static class RemoveDomainEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new RemoveDomainCommand(id, tenantId);
            await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new
            {
                message = "Domain removed successfully"
            }));
        })
        .WithName("RemoveDomain")
        .WithSummary("Remove a sending domain (soft delete)")
        .WithDescription("Soft deletes a domain. Fails if there are pending emails using this domain.")
        .WithOpenApi()
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
