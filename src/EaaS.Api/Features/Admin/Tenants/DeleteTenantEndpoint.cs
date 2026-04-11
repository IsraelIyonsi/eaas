using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Tenants;

public static class DeleteTenantEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);
            var command = new DeleteTenantCommand(adminUserId, id);
            await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new { message = "Tenant deactivated" }));
        })
        .WithName("DeleteTenant")
        .WithSummary("Delete a tenant")
        .WithDescription("Soft-deletes a tenant by setting status to Deactivated.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
