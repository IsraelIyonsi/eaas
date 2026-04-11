using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Plans;

public sealed class UpdatePlanHandler : IRequestHandler<UpdatePlanCommand, PlanResult>
{
    private readonly AppDbContext _dbContext;

    public UpdatePlanHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlanResult> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.Plans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            throw new NotFoundException("Plan not found");

        if (request.Name is not null)
        {
            var nameExists = await _dbContext.Plans
                .AnyAsync(p => p.Name == request.Name && p.Id != request.PlanId, cancellationToken);

            if (nameExists)
                throw new ConflictException($"A plan with name '{request.Name}' already exists.");

            plan.Name = request.Name;
        }

        if (request.Tier.HasValue) plan.Tier = request.Tier.Value;
        if (request.MonthlyPriceUsd.HasValue) plan.MonthlyPriceUsd = request.MonthlyPriceUsd.Value;
        if (request.AnnualPriceUsd.HasValue) plan.AnnualPriceUsd = request.AnnualPriceUsd.Value;
        if (request.DailyEmailLimit.HasValue) plan.DailyEmailLimit = request.DailyEmailLimit.Value;
        if (request.MonthlyEmailLimit.HasValue) plan.MonthlyEmailLimit = request.MonthlyEmailLimit.Value;
        if (request.MaxApiKeys.HasValue) plan.MaxApiKeys = request.MaxApiKeys.Value;
        if (request.MaxDomains.HasValue) plan.MaxDomains = request.MaxDomains.Value;
        if (request.MaxTemplates.HasValue) plan.MaxTemplates = request.MaxTemplates.Value;
        if (request.MaxWebhooks.HasValue) plan.MaxWebhooks = request.MaxWebhooks.Value;
        if (request.CustomDomainBranding.HasValue) plan.CustomDomainBranding = request.CustomDomainBranding.Value;
        if (request.PrioritySupport.HasValue) plan.PrioritySupport = request.PrioritySupport.Value;
        if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;

        plan.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlanResult(
            plan.Id,
            plan.Name,
            plan.Tier.ToString().ToLowerInvariant(),
            plan.MonthlyPriceUsd,
            plan.AnnualPriceUsd,
            plan.DailyEmailLimit,
            plan.MonthlyEmailLimit,
            plan.MaxApiKeys,
            plan.MaxDomains,
            plan.MaxTemplates,
            plan.MaxWebhooks,
            plan.CustomDomainBranding,
            plan.PrioritySupport,
            plan.IsActive,
            plan.CreatedAt,
            plan.UpdatedAt);
    }
}
