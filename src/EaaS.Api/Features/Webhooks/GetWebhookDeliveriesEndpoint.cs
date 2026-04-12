using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Webhooks;

public static class GetWebhookDeliveriesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/deliveries", async (
            Guid id,
            HttpContext httpContext,
            IMediator mediator,
            int page = 1,
            int page_size = 20,
            bool? success = null) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetWebhookDeliveriesQuery(id, tenantId, page, page_size, success);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(new
            {
                items = result.Items,
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount
            }));
        })
        .WithName("GetWebhookDeliveries")
        .WithSummary("Get webhook delivery history")
        .WithDescription("Returns paginated delivery logs for the specified webhook. Optionally filter by success status.")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
