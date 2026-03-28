using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Emails;

public static class SendEmailEndpoint
{
    public sealed record SendEmailRequest(
        string From,
        List<string> To,
        string? Subject,
        string? HtmlBody,
        string? TextBody,
        Guid? TemplateId,
        Dictionary<string, object>? Variables,
        List<string>? Tags,
        Dictionary<string, string>? Metadata,
        string? IdempotencyKey);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/send", async (SendEmailRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var apiKeyId = GetApiKeyId(httpContext);

            var command = new SendEmailCommand(
                tenantId,
                apiKeyId,
                request.From,
                request.To,
                request.Subject,
                request.HtmlBody,
                request.TextBody,
                request.TemplateId,
                request.Variables,
                request.Tags,
                request.Metadata,
                request.IdempotencyKey);

            var result = await mediator.Send(command);

            return Results.Accepted($"/api/v1/emails/{result.MessageId}", ApiResponse.Ok(new
            {
                messageId = result.MessageId,
                status = result.Status
            }));
        })
        .WithName("SendEmail")
        .WithOpenApi()
        .Produces<ApiResponse<object>>(StatusCodes.Status202Accepted)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }

    private static Guid GetApiKeyId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst("ApiKeyId")?.Value;
        return claim is not null ? Guid.Parse(claim) : Guid.Empty;
    }
}
