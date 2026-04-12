using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Plans;

public sealed class ListBillingPlansHandler : IRequestHandler<ListBillingPlansQuery, List<BillingPlanResult>>
{
    private readonly AppDbContext _dbContext;

    public ListBillingPlansHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<BillingPlanResult>> Handle(ListBillingPlansQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Plans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPriceUsd)
            .Select(p => new BillingPlanResult(
                p.Id,
                p.Name,
                p.Tier.ToString().ToLowerInvariant(),
                p.MonthlyPriceUsd,
                p.AnnualPriceUsd,
                p.DailyEmailLimit,
                p.MonthlyEmailLimit,
                p.MaxApiKeys,
                p.MaxDomains,
                p.MaxTemplates,
                p.MaxWebhooks,
                p.CustomDomainBranding,
                p.PrioritySupport))
            .ToListAsync(cancellationToken);
    }
}
