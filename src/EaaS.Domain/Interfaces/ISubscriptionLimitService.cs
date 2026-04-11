namespace EaaS.Domain.Interfaces;

public interface ISubscriptionLimitService
{
    Task<bool> CanSendEmailAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> CanCreateApiKeyAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> CanAddDomainAsync(Guid tenantId, CancellationToken ct = default);
    Task<PlanLimits> GetLimitsAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record PlanLimits(
    int DailyEmailLimit,
    long MonthlyEmailLimit,
    int MaxApiKeys,
    int MaxDomains,
    int MaxTemplates,
    int MaxWebhooks);
