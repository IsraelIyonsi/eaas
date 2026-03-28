using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Emails;

public static class ListEmailsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            HttpContext httpContext,
            IMediator mediator,
            int page = 1,
            int pageSize = 20,
            string? status = null,
            DateTime? from = null,
            DateTime? to = null,
            string? tag = null) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new ListEmailsQuery(tenantId, page, pageSize, status, from, to, tag);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(new
            {
                items = result.Items,
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount
            }));
        })
        .WithName("ListEmails")
        .WithOpenApi()
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
