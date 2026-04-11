using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed class ListInvoicesHandler : IRequestHandler<ListInvoicesQuery, InvoiceListResult>
{
    private readonly AppDbContext _dbContext;

    public ListInvoicesHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InvoiceListResult> Handle(ListInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == request.TenantId)
            .OrderByDescending(i => i.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new InvoiceResult(
                i.Id,
                i.SubscriptionId,
                i.InvoiceNumber,
                i.AmountUsd,
                i.Currency,
                i.Status.ToString().ToLowerInvariant(),
                i.Provider.ToString().ToLowerInvariant(),
                i.PeriodStart,
                i.PeriodEnd,
                i.PaidAt,
                i.CreatedAt))
            .ToListAsync(cancellationToken);

        return new InvoiceListResult(items, request.Page, request.PageSize, totalCount);
    }
}
