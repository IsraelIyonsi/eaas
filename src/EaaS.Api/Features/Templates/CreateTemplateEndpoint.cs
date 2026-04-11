using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Templates;

public static class CreateTemplateEndpoint
{
    public sealed record CreateTemplateRequest(
        string Name,
        string SubjectTemplate,
        string HtmlBody,
        string? TextBody);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateTemplateRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            var command = new CreateTemplateCommand(
                tenantId,
                request.Name,
                request.SubjectTemplate,
                request.HtmlBody,
                request.TextBody);

            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/templates/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("CreateTemplate")
        .Produces<ApiResponse<TemplateResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
