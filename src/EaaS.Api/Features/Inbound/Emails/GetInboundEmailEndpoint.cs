using System.Text.Json;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Inbound.Emails;

public static class GetInboundEmailEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var tenantId = Guid.Parse(
                httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value ?? Guid.Empty.ToString());

            var email = await dbContext.InboundEmails
                .AsNoTracking()
                .Include(e => e.Attachments)
                .Where(e => e.Id == id && e.TenantId == tenantId)
                .FirstOrDefaultAsync(cancellationToken);

            if (email is null)
                return Results.NotFound(ApiErrorResponse.Create("NOT_FOUND", "Inbound email not found."));

            return Results.Ok(ApiResponse.Ok(new
            {
                email.Id,
                email.MessageId,
                email.FromEmail,
                email.FromName,
                ToEmails = ParseJson(email.ToEmails),
                CcEmails = ParseJson(email.CcEmails),
                email.ReplyTo,
                email.Subject,
                Status = email.Status.ToString().ToLowerInvariant(),
                email.HtmlBody,
                email.TextBody,
                email.Headers,
                email.S3Key,
                email.SpamVerdict,
                email.VirusVerdict,
                email.SpfVerdict,
                email.DkimVerdict,
                email.DmarcVerdict,
                email.InReplyTo,
                email.References,
                email.OutboundEmailId,
                email.ReceivedAt,
                email.ProcessedAt,
                email.CreatedAt,
                Attachments = email.Attachments.Select(a => new
                {
                    a.Id,
                    a.Filename,
                    a.ContentType,
                    a.SizeBytes,
                    a.ContentId,
                    a.IsInline,
                }),
            }));
        })
        .WithName("GetInboundEmail");
    }

    private static object? ParseJson(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return Array.Empty<object>();
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return Array.Empty<object>(); }
    }
}
