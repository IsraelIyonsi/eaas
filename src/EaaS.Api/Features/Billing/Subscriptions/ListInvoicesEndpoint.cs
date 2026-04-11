using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public static class ListInvoicesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/invoices", async (
            HttpContext httpContext,
            IMediator mediator,
            int page = 1,
            int pageSize = 20) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new ListInvoicesQuery(tenantId, page, pageSize);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListInvoices")
        .WithSummary("List invoices")
        .WithDescription("Returns paginated invoices for the authenticated tenant.")
        .Produces<ApiResponse<InvoiceListResult>>(StatusCodes.Status200OK);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
