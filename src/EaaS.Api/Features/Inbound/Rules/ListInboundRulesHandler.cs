using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed class ListInboundRulesHandler : IRequestHandler<ListInboundRulesQuery, PagedResponse<InboundRuleResult>>
{
    private readonly AppDbContext _dbContext;

    public ListInboundRulesHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResponse<InboundRuleResult>> Handle(ListInboundRulesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.InboundRules
            .AsNoTracking()
            .Where(r => r.TenantId == request.TenantId);

        if (request.DomainId.HasValue)
            query = query.Where(r => r.DomainId == request.DomainId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Min(request.PageSize, PaginationConstants.MaxPageSize);
        var items = await query
            .Join(
                _dbContext.Domains.AsNoTracking(),
                r => r.DomainId,
                d => d.Id,
                (r, d) => new { Rule = r, DomainName = d.DomainName })
            .OrderBy(x => x.Rule.Priority)
            .ThenBy(x => x.Rule.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InboundRuleResult(
                x.Rule.Id,
                x.Rule.Name,
                x.Rule.DomainId,
                x.DomainName,
                x.Rule.MatchPattern,
                x.Rule.Action.ToString(),
                x.Rule.WebhookUrl,
                x.Rule.ForwardTo,
                x.Rule.IsActive,
                x.Rule.Priority,
                x.Rule.CreatedAt,
                x.Rule.UpdatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResponse<InboundRuleResult>(items, totalCount, request.Page, pageSize, totalPages);
    }
}
