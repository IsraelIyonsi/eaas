namespace EaaS.Domain.Interfaces;

public record RateLimitResult(bool Allowed, int Remaining, long ResetAtUnixMs);

public interface IRateLimiter
{
    Task<bool> CheckRateLimitAsync(string key, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default);
    Task<RateLimitResult> CheckRateLimitWithInfoAsync(string key, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default);
}
