namespace EaaS.Domain.Interfaces;

public interface ICacheService
{
    Task<bool> IsEmailSuppressedAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default);
    Task AddToSuppressionCacheAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default);
    Task RemoveFromSuppressionCacheAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default);
    Task<bool> CheckRateLimitAsync(string key, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default);
    Task<string?> GetApiKeyCacheAsync(string keyHash, CancellationToken cancellationToken = default);
    Task SetApiKeyCacheAsync(string keyHash, string serializedApiKey, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task InvalidateApiKeyCacheAsync(string keyHash, CancellationToken cancellationToken = default);

    // Idempotency key support
    Task<string?> GetIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task SetIdempotencyKeyAsync(Guid tenantId, string key, string value, CancellationToken cancellationToken = default);

    // Template cache support
    Task<string?> GetTemplateCacheAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task SetTemplateCacheAsync(Guid templateId, string serializedTemplate, CancellationToken cancellationToken = default);
    Task InvalidateTemplateCacheAsync(Guid templateId, CancellationToken cancellationToken = default);
}
