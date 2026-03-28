using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Domains;

public static class VerifyDomainEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/verify", async (Guid id, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new VerifyDomainCommand(id, tenantId);
            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new
            {
                id = result.Id,
                domainName = result.DomainName,
                status = result.Status,
                dnsRecords = result.DnsRecords,
                verifiedAt = result.VerifiedAt
            }));
        })
        .WithName("VerifyDomain")
        .WithOpenApi()
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
