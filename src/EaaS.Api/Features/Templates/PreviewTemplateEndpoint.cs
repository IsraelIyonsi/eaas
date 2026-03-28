using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Templates;

public static class PreviewTemplateEndpoint
{
    public sealed record PreviewTemplateRequest(
        Dictionary<string, object>? Variables);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/preview", async (Guid id, PreviewTemplateRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            var command = new PreviewTemplateCommand(
                tenantId,
                id,
                request.Variables);

            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(new
            {
                subject = result.Subject,
                html_body = result.HtmlBody,
                text_body = result.TextBody
            }));
        })
        .WithName("PreviewTemplate")
        .WithSummary("Preview a rendered template")
        .WithDescription("Renders a template with the provided variables without sending an email.")
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
