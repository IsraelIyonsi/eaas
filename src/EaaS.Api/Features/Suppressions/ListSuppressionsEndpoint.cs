using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Suppressions;

public static class ListSuppressionsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            HttpContext httpContext,
            IMediator mediator,
            int page = 1,
            int pageSize = 20,
            string? reason = null,
            string? search = null) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new ListSuppressionsQuery(tenantId, page, pageSize, reason, search);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(new
            {
                items = result.Items,
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount
            }));
        })
        .WithName("ListSuppressions")
        .WithSummary("List suppressed email addresses")
        .WithDescription("Returns paginated list of suppressed email addresses with optional filtering by reason and search.")
        .WithOpenApi()
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
