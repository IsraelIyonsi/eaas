namespace EaaS.Domain.Interfaces;

public interface IRateLimiter
{
    Task<bool> CheckRateLimitAsync(string key, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default);
}
