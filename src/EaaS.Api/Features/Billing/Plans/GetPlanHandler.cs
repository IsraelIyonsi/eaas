using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Plans;

public sealed class GetPlanHandler : IRequestHandler<GetPlanQuery, PlanResult>
{
    private readonly AppDbContext _dbContext;

    public GetPlanHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlanResult> Handle(GetPlanQuery request, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Plans
            .AsNoTracking()
            .Where(p => p.Id == request.PlanId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            throw new NotFoundException("Plan not found");

        return result;
    }
}
