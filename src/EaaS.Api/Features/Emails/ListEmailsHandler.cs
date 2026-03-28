using System.Text.Json;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
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

        // Filter by tag
        if (!string.IsNullOrWhiteSpace(request.Tag))
            query = query.Where(e => e.Tags.Contains(request.Tag));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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

        return new ListEmailsResult(dtos, request.Page, request.PageSize, totalCount);
    }
}
