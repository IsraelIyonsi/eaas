using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed class UpdateInboundRuleHandler : IRequestHandler<UpdateInboundRuleCommand, InboundRuleResult>
{
    private readonly AppDbContext _dbContext;

    public UpdateInboundRuleHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InboundRuleResult> Handle(UpdateInboundRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.InboundRules
            .Where(r => r.Id == request.RuleId && r.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Inbound rule with id '{request.RuleId}' not found.");

        if (request.Name is not null && request.Name != rule.Name)
        {
            var nameExists = await _dbContext.InboundRules
                .AsNoTracking()
                .AnyAsync(r => r.TenantId == request.TenantId && r.Name == request.Name && r.Id != request.RuleId, cancellationToken);

            if (nameExists)
                throw new ConflictException("Rule name already exists");
        }

        if (request.Name is not null)
            rule.Name = request.Name;

        if (request.MatchPattern is not null)
            rule.MatchPattern = request.MatchPattern;

        if (request.Action.HasValue)
            rule.Action = request.Action.Value;

        if (request.WebhookUrl is not null)
            rule.WebhookUrl = request.WebhookUrl;

        if (request.ForwardTo is not null)
            rule.ForwardTo = request.ForwardTo;

        if (request.IsActive.HasValue)
            rule.IsActive = request.IsActive.Value;

        if (request.Priority.HasValue)
            rule.Priority = request.Priority.Value;

        rule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var domainName = await _dbContext.Domains
            .AsNoTracking()
            .Where(d => d.Id == rule.DomainId)
            .Select(d => d.DomainName)
            .FirstAsync(cancellationToken);

        return new InboundRuleResult(
            rule.Id,
            rule.Name,
            rule.DomainId,
            domainName,
            rule.MatchPattern,
            rule.Action.ToString(),
            rule.WebhookUrl,
            rule.ForwardTo,
            rule.IsActive,
            rule.Priority,
            rule.CreatedAt,
            rule.UpdatedAt);
    }
}
