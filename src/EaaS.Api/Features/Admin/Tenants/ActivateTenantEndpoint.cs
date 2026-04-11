using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Tenants;

public static class ActivateTenantEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/activate", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);
            var command = new ActivateTenantCommand(adminUserId, id);
            await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new { message = "Tenant activated" }));
        })
        .WithName("ActivateTenant")
        .WithSummary("Activate a tenant")
        .WithDescription("Reactivates a suspended or deactivated tenant.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
