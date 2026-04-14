using EaaS.Domain.Interfaces;

namespace EaaS.Api.Tests.Features.PasswordReset;

/// <summary>
/// In-memory fake of <see cref="IPasswordResetTokenStore"/> for handler tests.
/// Does not enforce TTL expiry (tests explicitly invoke Expire when needed).
/// </summary>
public sealed class InMemoryPasswordResetTokenStore : IPasswordResetTokenStore
{
    public Dictionary<string, (Guid TenantId, string Email, DateTimeOffset ExpiresAt)> Tokens { get; } = new();
    public Dictionary<string, (long Count, DateTimeOffset ExpiresAt)> RateLimits { get; } = new();

    public Task StoreTokenAsync(string tokenHash, Guid tenantId, string email, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        Tokens[tokenHash] = (tenantId, email, DateTimeOffset.UtcNow + ttl);
        return Task.CompletedTask;
    }

    public Task<PasswordResetTokenPayload?> ConsumeTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (!Tokens.TryGetValue(tokenHash, out var entry))
            return Task.FromResult<PasswordResetTokenPayload?>(null);

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            Tokens.Remove(tokenHash);
            return Task.FromResult<PasswordResetTokenPayload?>(null);
        }

        Tokens.Remove(tokenHash);
        return Task.FromResult<PasswordResetTokenPayload?>(
            new PasswordResetTokenPayload(entry.TenantId, entry.Email));
    }

    public Task<long> IncrementRateLimitAsync(string bucketKey, TimeSpan window, CancellationToken cancellationToken = default)
    {
        if (RateLimits.TryGetValue(bucketKey, out var entry) && entry.ExpiresAt >= DateTimeOffset.UtcNow)
        {
            var next = entry.Count + 1;
            RateLimits[bucketKey] = (next, entry.ExpiresAt);
            return Task.FromResult(next);
        }

        RateLimits[bucketKey] = (1, DateTimeOffset.UtcNow + window);
        return Task.FromResult(1L);
    }

    public void ExpireToken(string tokenHash)
    {
        if (Tokens.TryGetValue(tokenHash, out var entry))
            Tokens[tokenHash] = (entry.TenantId, entry.Email, DateTimeOffset.UtcNow.AddSeconds(-1));
    }
}
