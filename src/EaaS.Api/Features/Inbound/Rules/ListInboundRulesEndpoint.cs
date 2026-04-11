using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Inbound.Rules;

public static class ListInboundRulesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            HttpContext httpContext,
            IMediator mediator,
            int page = 1,
            int pageSize = 20,
            Guid? domainId = null) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new ListInboundRulesQuery(tenantId, page, pageSize, domainId);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListInboundRules")
        .WithSummary("List inbound rules")
        .WithDescription("Returns paginated list of inbound email routing rules for the tenant.")
        .Produces<ApiResponse<PagedResponse<InboundRuleResult>>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
