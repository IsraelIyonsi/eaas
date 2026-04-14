using EaaS.Shared.Contracts;
using MediatR;
using static EaaS.Api.Features.Admin.AdminEndpointHelpers;

namespace EaaS.Api.Features.Admin.Tenants;

public static class CreateTenantEndpoint
{
    public sealed record CreateTenantRequest(
        string Name,
        string? ContactEmail,
        string? CompanyName,
        string LegalEntityName,
        string PostalAddress,
        int? MaxApiKeys,
        int? MaxDomainsCount,
        long? MonthlyEmailLimit,
        string? Notes);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateTenantRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var adminUserId = GetAdminUserId(httpContext);

            var command = new CreateTenantCommand(
                adminUserId,
                request.Name,
                request.ContactEmail,
                request.CompanyName,
                request.LegalEntityName,
                request.PostalAddress,
                request.MaxApiKeys,
                request.MaxDomainsCount,
                request.MonthlyEmailLimit,
                request.Notes);

            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/admin/tenants/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("CreateTenant")
        .WithSummary("Create a new tenant")
        .WithDescription("Creates a new tenant on the platform.")
        .Produces<ApiResponse<TenantDetailResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }
}
