using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed class GetSubscriptionHandler : IRequestHandler<GetSubscriptionQuery, SubscriptionResult?>
{
    private readonly AppDbContext _dbContext;

    public GetSubscriptionHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SubscriptionResult?> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == request.TenantId
                        && s.Status != SubscriptionStatus.Cancelled
                        && s.Status != SubscriptionStatus.Expired)
            .Select(s => new SubscriptionResult(
                s.Id,
                s.PlanId,
                s.Plan.Name,
                s.Plan.Tier.ToString().ToLowerInvariant(),
                s.Status.ToString().ToLowerInvariant(),
                s.Provider.ToString().ToLowerInvariant(),
                s.CurrentPeriodStart,
                s.CurrentPeriodEnd,
                s.TrialEndsAt,
                s.CancelledAt))
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }
}
