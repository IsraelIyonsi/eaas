using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Tenants;

public static class SuspendTenantEndpoint
{
    public sealed record SuspendTenantRequest(string? Reason);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/suspend", async (Guid id, SuspendTenantRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);
            var command = new SuspendTenantCommand(adminUserId, id, request.Reason);
            await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new { message = "Tenant suspended" }));
        })
        .WithName("SuspendTenant")
        .WithSummary("Suspend a tenant")
        .WithDescription("Suspends a tenant, preventing API access.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
