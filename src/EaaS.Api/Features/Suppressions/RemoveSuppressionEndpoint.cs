using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Suppressions;

public static class RemoveSuppressionEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new RemoveSuppressionCommand(id, tenantId);
            await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new
            {
                message = "Suppression removed successfully"
            }));
        })
        .WithName("RemoveSuppression")
        .WithSummary("Remove an email address from the suppression list")
        .WithDescription("Removes a suppression entry from the database and Redis cache.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
