using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Domains;

public static class ListDomainsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new ListDomainsQuery(tenantId);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListDomains")
        .Produces<ApiResponse<IReadOnlyList<DomainSummary>>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
