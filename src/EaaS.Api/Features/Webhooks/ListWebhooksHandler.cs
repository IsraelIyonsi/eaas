using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Webhooks;

public sealed class ListWebhooksHandler : IRequestHandler<ListWebhooksQuery, ListWebhooksResult>
{
    private readonly AppDbContext _dbContext;

    public ListWebhooksHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ListWebhooksResult> Handle(ListWebhooksQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Webhooks
            .AsNoTracking()
            .Where(w => w.TenantId == request.TenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Min(request.PageSize, 100);
        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WebhookDto(
                w.Id,
                w.Url,
                w.Events,
                w.Status,
                w.CreatedAt,
                w.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new ListWebhooksResult(items, request.Page, pageSize, totalCount);
    }
}
