using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Plans;

public sealed class ListPlansHandler : IRequestHandler<ListPlansQuery, List<PlanResult>>
{
    private readonly AppDbContext _dbContext;

    public ListPlansHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<PlanResult>> Handle(ListPlansQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Plans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPriceUsd)
            .Select(p => new PlanResult(
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
                p.PrioritySupport,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}
