using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Domains;

public static class GetDomainEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetDomainQuery(id, tenantId);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetDomain")
        .WithSummary("Get a sending domain by ID")
        .WithDescription("Returns full domain detail including DNS records.")
        .Produces<ApiResponse<DomainSummary>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
