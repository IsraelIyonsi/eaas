using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Emails;

public static class GetEmailEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        // BUG-M3: route constraint removed so we can accept EITHER the internal GUID
        // or the public `snx_`-prefixed MessageId returned by POST /emails. The handler
        // dispatches on the prefix.
        group.MapGet("/{id}", async (string id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetEmailQuery(tenantId, id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetEmail")
        .WithSummary("Get email detail")
        .WithDescription("Returns a single email by its internal UUID or its public snx_ message id.")
        .Produces<ApiResponse<EmailDetailResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
