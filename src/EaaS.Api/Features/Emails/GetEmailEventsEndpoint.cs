using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Emails;

public static class GetEmailEventsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/events", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetEmailEventsQuery(tenantId, id);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetEmailEvents")
        .WithSummary("Get events for an email")
        .WithDescription("Returns all tracking events for the specified email, ordered by time.")
        .Produces<ApiResponse<List<EmailEventDto>>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
