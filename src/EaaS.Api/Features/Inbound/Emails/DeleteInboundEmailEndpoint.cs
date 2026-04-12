using EaaS.Shared.Contracts;

namespace EaaS.Api.Features.Inbound.Emails;

public static class DeleteInboundEmailEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            DeleteInboundEmailHandler handler,
            CancellationToken cancellationToken) =>
        {
            var tenantId = GetTenantId(httpContext);
            await handler.HandleAsync(id, tenantId, cancellationToken);

            return Results.Ok(ApiResponse.Ok(new
            {
                message = "Inbound email deleted successfully."
            }));
        })
        .WithName("DeleteInboundEmail")
        .WithSummary("Delete an inbound email")
        .WithDescription("Hard deletes an inbound email record for the authenticated tenant.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
