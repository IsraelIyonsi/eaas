using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Templates;

public static class RollbackTemplateEndpoint
{
    public sealed record RollbackTemplateRequest(int Version);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/rollback", async (
            Guid id,
            RollbackTemplateRequest request,
            HttpContext httpContext,
            IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new RollbackTemplateCommand(tenantId, id, request.Version);
            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("RollbackTemplate")
        .Produces<ApiResponse<TemplateResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
