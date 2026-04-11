using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Inbound.Rules;

public static class GetInboundRuleEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetInboundRuleQuery(tenantId, id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetInboundRule")
        .WithSummary("Get inbound rule detail")
        .WithDescription("Returns a single inbound rule by ID.")
        .Produces<ApiResponse<InboundRuleResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
