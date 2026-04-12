using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed class CancelSubscriptionHandler : IRequestHandler<CancelSubscriptionCommand, SubscriptionResult>
{
    private readonly AppDbContext _dbContext;

    public CancelSubscriptionHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SubscriptionResult> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _dbContext.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == request.TenantId
                && s.Status != SubscriptionStatus.Cancelled
                && s.Status != SubscriptionStatus.Expired, cancellationToken);

        if (subscription is null)
            throw new NotFoundException("No subscription found for this tenant.");

        if (subscription.Status == SubscriptionStatus.Cancelled)
            throw new ValidationException("Subscription is already cancelled.");

        var now = DateTime.UtcNow;
        subscription.CancelledAt = now;
        subscription.UpdatedAt = now;

        if (request.Immediate)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CurrentPeriodEnd = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SubscriptionResult(
            subscription.Id,
            subscription.PlanId,
            subscription.Plan.Name,
            subscription.Plan.Tier.ToString().ToLowerInvariant(),
            subscription.Status.ToString().ToLowerInvariant(),
            subscription.Provider.ToString().ToLowerInvariant(),
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialEndsAt,
            subscription.CancelledAt);
    }
}
