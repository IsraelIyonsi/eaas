using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.AuditLogs;

public sealed class ListAuditLogsHandler : IRequestHandler<ListAuditLogsQuery, PagedResponse<AuditLogResult>>
{
    private readonly AppDbContext _dbContext;

    public ListAuditLogsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResponse<AuditLogResult>> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.AdminUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Action) &&
            Enum.TryParse<AuditAction>(request.Action, ignoreCase: true, out var action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (request.AdminUserId.HasValue)
            query = query.Where(a => a.AdminUserId == request.AdminUserId.Value);

        if (request.From.HasValue)
            query = query.Where(a => a.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(a => a.CreatedAt <= request.To.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageSize = Math.Min(request.PageSize, PaginationConstants.MaxPageSize);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogResult(
                a.Id,
                a.AdminUserId,
                a.AdminUser.Email,
                a.Action.ToString(),
                a.TargetType,
                a.TargetId,
                a.Details,
                a.IpAddress,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResponse<AuditLogResult>(items, totalCount, request.Page, pageSize, totalPages);
    }
}
