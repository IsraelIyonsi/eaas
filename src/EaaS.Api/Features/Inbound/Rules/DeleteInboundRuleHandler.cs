using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed class DeleteInboundRuleHandler : IRequestHandler<DeleteInboundRuleCommand>
{
    private readonly AppDbContext _dbContext;

    public DeleteInboundRuleHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteInboundRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.InboundRules
            .Where(r => r.Id == request.RuleId && r.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Inbound rule with id '{request.RuleId}' not found.");

        _dbContext.InboundRules.Remove(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
