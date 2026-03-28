using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class ListTemplatesHandler : IRequestHandler<ListTemplatesQuery, ListTemplatesResult>
{
    private readonly AppDbContext _dbContext;

    public ListTemplatesHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ListTemplatesResult> Handle(ListTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Templates
            .AsNoTracking()
            .Where(t => t.TenantId == request.TenantId && t.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search}%";
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TemplateSummaryDto(
                t.Id,
                t.Name,
                t.SubjectTemplate,
                t.Version,
                t.CreatedAt,
                t.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new ListTemplatesResult(items, request.Page, request.PageSize, totalCount);
    }
}
