using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed class GetInboundRuleHandler : IRequestHandler<GetInboundRuleQuery, InboundRuleResult>
{
    private readonly AppDbContext _dbContext;

    public GetInboundRuleHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InboundRuleResult> Handle(GetInboundRuleQuery request, CancellationToken cancellationToken)
    {
        var result = await _dbContext.InboundRules
            .AsNoTracking()
            .Where(r => r.Id == request.RuleId && r.TenantId == request.TenantId)
            .Join(
                _dbContext.Domains.AsNoTracking(),
                r => r.DomainId,
                d => d.Id,
                (r, d) => new InboundRuleResult(
                    r.Id,
                    r.Name,
                    r.DomainId,
                    d.DomainName,
                    r.MatchPattern,
                    r.Action.ToString(),
                    r.WebhookUrl,
                    r.ForwardTo,
                    r.IsActive,
                    r.Priority,
                    r.CreatedAt,
                    r.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Inbound rule with id '{request.RuleId}' not found.");

        return result;
    }
}
