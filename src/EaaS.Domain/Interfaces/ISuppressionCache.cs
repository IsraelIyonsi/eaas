namespace EaaS.Domain.Interfaces;

public interface ISuppressionCache
{
    Task<bool> IsEmailSuppressedAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default);
    Task AddToSuppressionCacheAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default);
    Task RemoveFromSuppressionCacheAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default);
}
