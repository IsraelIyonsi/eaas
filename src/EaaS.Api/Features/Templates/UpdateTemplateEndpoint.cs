using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Templates;

public static class UpdateTemplateEndpoint
{
    public sealed record UpdateTemplateRequest(
        string? Name,
        string? SubjectTemplate,
        string? HtmlBody,
        string? TextBody);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateTemplateRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            var command = new UpdateTemplateCommand(
                tenantId,
                id,
                request.Name,
                request.SubjectTemplate,
                request.HtmlBody,
                request.TextBody);

            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("UpdateTemplate")
        .Produces<ApiResponse<TemplateResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
