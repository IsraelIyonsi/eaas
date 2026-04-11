using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Emails;

public static class ScheduleEmailEndpoint
{
    public sealed record ScheduleEmailRequest(
        string From,
        string To,
        string Subject,
        string? HtmlBody,
        string? TextBody,
        Guid? TemplateId,
        Dictionary<string, string>? Variables,
        DateTime ScheduledAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/schedule", async (ScheduleEmailRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var apiKeyId = GetApiKeyId(httpContext);

            var command = new ScheduleEmailCommand(
                tenantId,
                apiKeyId,
                request.From,
                request.To,
                request.Subject,
                request.HtmlBody,
                request.TextBody,
                request.TemplateId,
                request.Variables,
                request.ScheduledAt);

            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/emails/{result.EmailId}", ApiResponse.Ok(result));
        })
        .WithName("ScheduleEmail")
        .Produces<ApiResponse<ScheduleEmailResult>>(StatusCodes.Status201Created)
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
