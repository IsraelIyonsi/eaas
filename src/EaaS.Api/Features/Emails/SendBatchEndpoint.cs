using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Emails;

public static class SendBatchEndpoint
{
    public sealed record SendBatchRequest(List<BatchEmailItemRequest> Emails);

    public sealed record BatchEmailItemRequest(
        string From,
        List<string> To,
        List<string>? Cc,
        List<string>? Bcc,
        string? Subject,
        string? HtmlBody,
        string? TextBody,
        Guid? TemplateId,
        Dictionary<string, object>? Variables,
        List<string>? Tags,
        Dictionary<string, string>? Metadata);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/batch", async (SendBatchRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var apiKeyId = GetApiKeyId(httpContext);

            var emailItems = request.Emails.Select(e => new BatchEmailItem(
                e.From, e.To, e.Cc, e.Bcc, e.Subject, e.HtmlBody, e.TextBody,
                e.TemplateId, e.Variables, e.Tags, e.Metadata)).ToList();

            var command = new SendBatchCommand(tenantId, apiKeyId, emailItems);
            var result = await mediator.Send(command);

            return Results.Accepted(null as string, ApiResponse.Ok(new
            {
                batch_id = result.BatchId,
                total = result.Total,
                accepted = result.Accepted,
                rejected = result.Rejected,
                messages = result.Messages.Select(m => new
                {
                    index = m.Index,
                    message_id = m.MessageId,
                    status = m.Status,
                    error = m.Error
                })
            }));
        })
        .WithName("SendBatchEmail")
        .WithSummary("Send a batch of emails")
        .WithDescription("Sends up to 100 emails in a single API call. Each email is validated and queued independently. Partial success is allowed.")
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
