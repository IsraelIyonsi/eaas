using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed class CreateSubscriptionHandler : IRequestHandler<CreateSubscriptionCommand, SubscriptionResult>
{
    private readonly AppDbContext _dbContext;

    public CreateSubscriptionHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SubscriptionResult> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, cancellationToken);

        if (plan is null)
            throw new NotFoundException($"Plan '{request.PlanId}' not found or inactive.");

        var hasActive = await _dbContext.Subscriptions
            .AnyAsync(s => s.TenantId == request.TenantId
                           && s.Status != SubscriptionStatus.Cancelled
                           && s.Status != SubscriptionStatus.Expired, cancellationToken);

        if (hasActive)
            throw new ConflictException("Tenant already has an active subscription. Cancel the existing subscription first.");

        var now = DateTime.UtcNow;
        var isFree = plan.Tier == PlanTier.Free;

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            PlanId = request.PlanId,
            Status = isFree ? SubscriptionStatus.Active : SubscriptionStatus.Trial,
            Provider = isFree ? PaymentProvider.None : ParseProvider(request.Provider),
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(30),
            TrialEndsAt = isFree ? null : now.AddDays(14),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SubscriptionResult(
            subscription.Id,
            subscription.PlanId,
            plan.Name,
            plan.Tier.ToString().ToLowerInvariant(),
            subscription.Status.ToString().ToLowerInvariant(),
            subscription.Provider.ToString().ToLowerInvariant(),
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialEndsAt,
            subscription.CancelledAt);
    }

    private static PaymentProvider ParseProvider(string? provider)
    {
        if (string.IsNullOrEmpty(provider))
            return PaymentProvider.PayStack;

        return Enum.TryParse<PaymentProvider>(provider, ignoreCase: true, out var result)
            ? result
            : PaymentProvider.PayStack;
    }
}
