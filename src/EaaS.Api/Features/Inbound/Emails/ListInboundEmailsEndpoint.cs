using System.Text.Json;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Inbound.Emails;

public static class ListInboundEmailsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            HttpContext httpContext,
            AppDbContext dbContext,
            int page = 1,
            int pageSize = 20,
            string? status = null,
            string? from = null,
            string? to = null,
            CancellationToken cancellationToken = default) =>
        {
            var tenantId = Guid.Parse(
                httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value ?? Guid.Empty.ToString());

            var query = dbContext.InboundEmails
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId);

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<Domain.Enums.InboundEmailStatus>(status, true, out var s))
                    query = query.Where(e => e.Status == s);
            }

            if (!string.IsNullOrEmpty(from))
                query = query.Where(e => e.FromEmail.Contains(from));

            if (!string.IsNullOrEmpty(to))
                query = query.Where(e => e.ToEmails.Contains(to));

            var totalCount = await query.CountAsync(cancellationToken);

            var rawItems = await query
                .OrderByDescending(e => e.ReceivedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(e => e.Attachments)
                .ToListAsync(cancellationToken);

            var items = rawItems.Select(e => new
            {
                e.Id,
                e.MessageId,
                e.FromEmail,
                e.FromName,
                ToEmails = ParseJsonArray(e.ToEmails),
                CcEmails = ParseJsonArray(e.CcEmails),
                e.Subject,
                Status = e.Status.ToString().ToLowerInvariant(),
                e.SpamVerdict,
                e.VirusVerdict,
                e.SpfVerdict,
                e.DkimVerdict,
                e.DmarcVerdict,
                e.InReplyTo,
                e.OutboundEmailId,
                e.ReceivedAt,
                e.ProcessedAt,
                e.CreatedAt,
                AttachmentCount = e.Attachments.Count,
            });

            return Results.Ok(ApiResponse.Ok(new
            {
                items,
                totalCount,
                page,
                pageSize,
            }));
        })
        .WithName("ListInboundEmails");
    }

    private static object? ParseJsonArray(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return Array.Empty<object>();
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return Array.Empty<object>(); }
    }
}
