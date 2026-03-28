using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Suppressions;

public sealed class ListSuppressionsHandler : IRequestHandler<ListSuppressionsQuery, ListSuppressionsResult>
{
    private readonly AppDbContext _dbContext;

    public ListSuppressionsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ListSuppressionsResult> Handle(ListSuppressionsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.SuppressionEntries
            .AsNoTracking()
            .Where(s => s.TenantId == request.TenantId);

        if (!string.IsNullOrWhiteSpace(request.Reason)
            && Enum.TryParse<SuppressionReason>(request.Reason, true, out var reasonEnum))
        {
            query = query.Where(s => s.Reason == reasonEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search}%";
            query = query.Where(s => EF.Functions.ILike(s.EmailAddress, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Min(request.PageSize, 100);
        var items = await query
            .OrderByDescending(s => s.SuppressedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SuppressionDto(
                s.Id,
                s.EmailAddress,
                s.Reason.ToString().ToLowerInvariant(),
                s.SourceMessageId,
                s.SuppressedAt))
            .ToListAsync(cancellationToken);

        return new ListSuppressionsResult(items, request.Page, pageSize, totalCount);
    }
}
