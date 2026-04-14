using System.Text.Json;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Data;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Emails;

public sealed class ListEmailsHandler : IRequestHandler<ListEmailsQuery, ListEmailsResult>
{
    private readonly AppDbContext _dbContext;

    public ListEmailsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ListEmailsResult> Handle(ListEmailsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Emails
            .AsNoTracking()
            .Where(e => e.TenantId == request.TenantId);

        // Filter by status
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<EmailStatus>(request.Status, true, out var statusEnum))
        {
            query = query.Where(e => e.Status == statusEnum);
        }

        // Filter by date range
        if (request.From.HasValue)
            query = query.Where(e => e.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(e => e.CreatedAt <= request.To.Value);

        // Filter by single tag (backward compatible)
        if (!string.IsNullOrWhiteSpace(request.Tag))
            query = query.Where(e => e.Tags.Contains(request.Tag));

        // Filter by comma-separated tags (any match)
        if (!string.IsNullOrWhiteSpace(request.Tags))
        {
            var tagList = request.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var tag in tagList)
            {
                var t = tag;
                query = query.Where(e => e.Tags.Contains(t));
            }
        }

        // Filter by from email (partial match).
        // User input is escaped so '%'/'_'/'\' are matched literally (H4).
        if (!string.IsNullOrWhiteSpace(request.FromEmail))
        {
            var fromPattern = $"%{SqlLikeEscape.Escape(request.FromEmail)}%";
            query = query.Where(e => EF.Functions.ILike(e.FromEmail, fromPattern, SqlLikeEscape.EscapeCharacter));
        }

        // Filter by to email (partial match in JSON).
        // User input is escaped so '%'/'_'/'\' are matched literally (H4).
        if (!string.IsNullOrWhiteSpace(request.ToEmail))
        {
            var toPattern = $"%{SqlLikeEscape.Escape(request.ToEmail)}%";
            query = query.Where(e => EF.Functions.ILike(e.ToEmails, toPattern, SqlLikeEscape.EscapeCharacter));
        }

        // Filter by template ID
        if (request.TemplateId.HasValue)
            query = query.Where(e => e.TemplateId == request.TemplateId.Value);

        // Filter by batch ID
        if (!string.IsNullOrWhiteSpace(request.BatchId))
            query = query.Where(e => e.BatchId == request.BatchId);

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        var sortBy = request.SortBy?.ToLowerInvariant() ?? "created_at";
        var sortDesc = string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? false : true;

        query = sortBy switch
        {
            "sent_at" => sortDesc ? query.OrderByDescending(e => e.SentAt) : query.OrderBy(e => e.SentAt),
            "status" => sortDesc ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status),
            _ => sortDesc ? query.OrderByDescending(e => e.CreatedAt) : query.OrderBy(e => e.CreatedAt)
        };

        var pageSize = Math.Min(request.PageSize, PaginationConstants.MaxPageSize);
        var items = await query
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(e => new EmailSummaryDto(
            e.Id,
            e.MessageId,
            e.FromEmail,
            JsonSerializer.Deserialize<List<string>>(e.ToEmails) ?? new List<string>(),
            e.Subject,
            e.Status.ToString().ToLowerInvariant(),
            e.Tags,
            e.CreatedAt,
            e.SentAt,
            e.DeliveredAt)).ToList();

        return new ListEmailsResult(dtos, request.Page, pageSize, totalCount);
    }
}
