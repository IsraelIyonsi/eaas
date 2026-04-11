using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Domains;

public static class AddDomainEndpoint
{
    public sealed record AddDomainRequest(string DomainName);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (AddDomainRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new AddDomainCommand(request.DomainName, tenantId);
            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/domains/{result.Id}", ApiResponse.Ok(new
            {
                id = result.Id,
                domainName = result.DomainName,
                status = result.Status,
                dnsRecords = result.DnsRecords,
                createdAt = result.CreatedAt
            }));
        })
        .WithName("AddDomain")
        .Produces<ApiResponse<object>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
