using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed class ListTenantsHandler : IRequestHandler<ListTenantsQuery, PagedResponse<TenantSummaryResult>>
{
    private readonly AppDbContext _dbContext;

    public ListTenantsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResponse<TenantSummaryResult>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Tenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<TenantStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(t =>
                EF.Functions.ILike(t.Name, $"%{search}%") ||
                (t.CompanyName != null && EF.Functions.ILike(t.CompanyName, $"%{search}%")) ||
                (t.ContactEmail != null && EF.Functions.ILike(t.ContactEmail, $"%{search}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageSize = Math.Min(request.PageSize, PaginationConstants.MaxPageSize);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TenantSummaryResult(
                t.Id,
                t.Name,
                t.Status.ToString().ToLowerInvariant(),
                t.CompanyName,
                t.ContactEmail,
                t.ApiKeys.Count,
                t.Domains.Count,
                t.Emails.Count,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResponse<TenantSummaryResult>(items, totalCount, request.Page, pageSize, totalPages);
    }
}
