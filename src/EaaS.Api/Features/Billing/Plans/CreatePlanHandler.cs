using EaaS.Domain.Entities;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Billing.Plans;

public sealed class CreatePlanHandler : IRequestHandler<CreatePlanCommand, PlanResult>
{
    private readonly AppDbContext _dbContext;

    public CreatePlanHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlanResult> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
    {
        var nameExists = await _dbContext.Plans
            .AnyAsync(p => p.Name == request.Name, cancellationToken);

        if (nameExists)
            throw new ConflictException($"A plan with name '{request.Name}' already exists.");

        var now = DateTime.UtcNow;

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Tier = request.Tier,
            MonthlyPriceUsd = request.MonthlyPriceUsd,
            AnnualPriceUsd = request.AnnualPriceUsd,
            DailyEmailLimit = request.DailyEmailLimit,
            MonthlyEmailLimit = request.MonthlyEmailLimit,
            MaxApiKeys = request.MaxApiKeys,
            MaxDomains = request.MaxDomains,
            MaxTemplates = request.MaxTemplates,
            MaxWebhooks = request.MaxWebhooks,
            CustomDomainBranding = request.CustomDomainBranding,
            PrioritySupport = request.PrioritySupport,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Plans.Add(plan);
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
