using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Webhooks;

public static class ListWebhooksEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            HttpContext httpContext,
            IMediator mediator,
            int page = 1,
            int pageSize = 20) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new ListWebhooksQuery(tenantId, page, pageSize);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(new
            {
                items = result.Items,
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount
            }));
        })
        .WithName("ListWebhooks")
        .WithSummary("List webhook endpoints")
        .WithDescription("Returns paginated list of webhook configurations for the tenant.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
