using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Templates;

public sealed class ListTemplateVersionsHandler : IRequestHandler<ListTemplateVersionsQuery, ListTemplateVersionsResult>
{
    private readonly AppDbContext _dbContext;

    public ListTemplateVersionsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ListTemplateVersionsResult> Handle(ListTemplateVersionsQuery request, CancellationToken cancellationToken)
    {
        var templateExists = await _dbContext.Templates
            .AsNoTracking()
            .AnyAsync(t => t.Id == request.TemplateId
                           && t.TenantId == request.TenantId
                           && t.DeletedAt == null, cancellationToken);

        if (!templateExists)
            throw new NotFoundException($"Template with ID '{request.TemplateId}' not found.");

        var query = _dbContext.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == request.TemplateId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(v => v.Version)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(v => new TemplateVersionResult(
                v.Id,
                v.Version,
                v.Name,
                v.Subject,
                v.HtmlBody,
                v.TextBody,
                v.Description,
                v.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ListTemplateVersionsResult(items, request.Page, request.PageSize, totalCount);
    }
}
