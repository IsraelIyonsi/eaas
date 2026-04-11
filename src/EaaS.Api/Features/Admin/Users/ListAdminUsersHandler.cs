using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Users;

public sealed class ListAdminUsersHandler : IRequestHandler<ListAdminUsersQuery, PagedResponse<AdminUserResult>>
{
    private readonly AppDbContext _dbContext;

    public ListAdminUsersHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResponse<AdminUserResult>> Handle(ListAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AdminUsers.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);
        var pageSize = Math.Min(request.PageSize, PaginationConstants.MaxPageSize);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserResult(
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role.ToString().ToLowerInvariant(),
                u.IsActive,
                u.LastLoginAt,
                u.CreatedAt,
                u.UpdatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResponse<AdminUserResult>(items, totalCount, request.Page, pageSize, totalPages);
    }
}
