using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Infrastructure.Services;

public sealed class SubscriptionLimitService : ISubscriptionLimitService
{
    private static readonly PlanLimits FreeDefaults = new(
        DailyEmailLimit: 100,
        MonthlyEmailLimit: 3000,
        MaxApiKeys: 3,
        MaxDomains: 2,
        MaxTemplates: 10,
        MaxWebhooks: 5);

    private readonly AppDbContext _dbContext;

    public SubscriptionLimitService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> CanSendEmailAsync(Guid tenantId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(tenantId, ct);
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var emailsThisMonth = await _dbContext.Emails
            .CountAsync(e => e.TenantId == tenantId && e.CreatedAt >= startOfMonth, ct);

        return emailsThisMonth < limits.MonthlyEmailLimit;
    }

    public async Task<bool> CanCreateApiKeyAsync(Guid tenantId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(tenantId, ct);

        var activeKeyCount = await _dbContext.ApiKeys
            .CountAsync(k => k.TenantId == tenantId && k.Status == ApiKeyStatus.Active, ct);

        return activeKeyCount < limits.MaxApiKeys;
    }

    public async Task<bool> CanAddDomainAsync(Guid tenantId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(tenantId, ct);

        var domainCount = await _dbContext.Domains
            .CountAsync(d => d.TenantId == tenantId && d.DeletedAt == null, ct);

        return domainCount < limits.MaxDomains;
    }

    public async Task<PlanLimits> GetLimitsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId
                        && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (subscription?.Plan is null)
            return FreeDefaults;

        var plan = subscription.Plan;
        return new PlanLimits(
            plan.DailyEmailLimit,
            plan.MonthlyEmailLimit,
            plan.MaxApiKeys,
            plan.MaxDomains,
            plan.MaxTemplates,
            plan.MaxWebhooks);
    }
}
