using EaaS.Domain.Entities;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed class CreateInboundRuleHandler : IRequestHandler<CreateInboundRuleCommand, InboundRuleResult>
{
    private readonly AppDbContext _dbContext;

    public CreateInboundRuleHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InboundRuleResult> Handle(CreateInboundRuleCommand request, CancellationToken cancellationToken)
    {
        var domainExists = await _dbContext.Domains
            .AsNoTracking()
            .AnyAsync(d => d.Id == request.DomainId && d.TenantId == request.TenantId && d.DeletedAt == null, cancellationToken);

        if (!domainExists)
            throw new NotFoundException("Domain not found");

        var nameExists = await _dbContext.InboundRules
            .AsNoTracking()
            .AnyAsync(r => r.TenantId == request.TenantId && r.Name == request.Name, cancellationToken);

        if (nameExists)
            throw new ConflictException("Rule name already exists");

        var now = DateTime.UtcNow;
        var rule = new InboundRule
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            DomainId = request.DomainId,
            Name = request.Name,
            MatchPattern = request.MatchPattern,
            Action = request.Action,
            WebhookUrl = request.WebhookUrl,
            ForwardTo = request.ForwardTo,
            IsActive = true,
            Priority = request.Priority,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.InboundRules.Add(rule);
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
