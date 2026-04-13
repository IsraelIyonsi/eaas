using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Templates;

public static class GetTemplateEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetTemplateQuery(tenantId, id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetTemplate")
        .Produces<ApiResponse<TemplateResult>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
