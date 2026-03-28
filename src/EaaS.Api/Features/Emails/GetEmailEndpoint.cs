using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Emails;

public static class GetEmailEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{messageId}", async (string messageId, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var query = new GetEmailQuery(tenantId, messageId);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("GetEmail")
        .WithOpenApi()
        .Produces<ApiResponse<EmailDetailResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
