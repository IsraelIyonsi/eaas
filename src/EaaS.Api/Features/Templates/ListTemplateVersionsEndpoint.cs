using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Templates;

public static class ListTemplateVersionsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/versions", async (
            Guid id,
            HttpContext httpContext,
            IMediator mediator,
            int page = 1,
            int pageSize = 20) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new ListTemplateVersionsQuery(tenantId, id, page, pageSize);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(new
            {
                items = result.Items,
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount
            }));
        })
        .WithName("ListTemplateVersions")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
