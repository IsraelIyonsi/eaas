namespace EaaS.Domain.Interfaces;

public interface IApiKeyCache
{
    Task<string?> GetApiKeyCacheAsync(string keyHash, CancellationToken cancellationToken = default);
    Task SetApiKeyCacheAsync(string keyHash, string serializedApiKey, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task InvalidateApiKeyCacheAsync(string keyHash, CancellationToken cancellationToken = default);
}
