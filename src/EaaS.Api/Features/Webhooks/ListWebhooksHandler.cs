using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
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

        var pageSize = Math.Min(request.PageSize, PaginationConstants.MaxPageSize);
        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WebhookDto(
                w.Id,
                w.Url,
                w.Events,
                w.Status.ToString(),
                w.CreatedAt,
                w.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new ListWebhooksResult(items, request.Page, pageSize, totalCount);
    }
}
