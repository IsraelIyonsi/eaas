using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Tenants;

public static class UpdateTenantEndpoint
{
    public sealed record UpdateTenantRequest(
        string? Name,
        string? ContactEmail,
        string? CompanyName,
        int? MaxApiKeys,
        int? MaxDomainsCount,
        long? MonthlyEmailLimit,
        string? Notes);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateTenantRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);

            var command = new UpdateTenantCommand(
                adminUserId,
                id,
                request.Name,
                request.ContactEmail,
                request.CompanyName,
                request.MaxApiKeys,
                request.MaxDomainsCount,
                request.MonthlyEmailLimit,
                request.Notes);

            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("UpdateTenant")
        .WithSummary("Update a tenant")
        .WithDescription("Updates tenant information.")
        .Produces<ApiResponse<TenantDetailResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
