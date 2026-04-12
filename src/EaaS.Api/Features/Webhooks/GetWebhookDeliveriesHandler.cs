using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Webhooks;

public sealed class GetWebhookDeliveriesHandler : IRequestHandler<GetWebhookDeliveriesQuery, WebhookDeliveriesResult>
{
    private readonly AppDbContext _dbContext;

    public GetWebhookDeliveriesHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WebhookDeliveriesResult> Handle(GetWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var webhookExists = await _dbContext.Webhooks
            .AsNoTracking()
            .AnyAsync(w => w.Id == request.WebhookId && w.TenantId == request.TenantId, cancellationToken);

        if (!webhookExists)
            throw new NotFoundException($"Webhook with id '{request.WebhookId}' not found.");

        var query = _dbContext.WebhookDeliveryLogs
            .AsNoTracking()
            .Where(d => d.WebhookId == request.WebhookId);

        if (request.Success.HasValue)
            query = query.Where(d => d.Success == request.Success.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Min(request.PageSize, PaginationConstants.MaxPageSize);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new WebhookDeliveryDto(
                d.Id,
                d.WebhookId,
                d.EmailId,
                d.EventType,
                d.StatusCode,
                d.Success,
                d.ErrorMessage,
                d.AttemptNumber,
                d.CreatedAt))
            .ToListAsync(cancellationToken);

        return new WebhookDeliveriesResult(items, request.Page, pageSize, totalCount);
    }
}
