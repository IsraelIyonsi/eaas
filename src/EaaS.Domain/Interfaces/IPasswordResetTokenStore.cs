namespace EaaS.Domain.Interfaces;

/// <summary>
/// Abstraction over the password reset token store.
/// Implementations persist single-use tokens with a TTL, and enforce per-email/per-IP rate limits.
/// </summary>
public interface IPasswordResetTokenStore
{
    /// <summary>
    /// Stores a token keyed by its hash. The value associates it with a tenant + email.
    /// </summary>
    Task StoreTokenAsync(string tokenHash, Guid tenantId, string email, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes a token: returns the associated payload (tenantId + email) and deletes it, or null if not present.
    /// </summary>
    Task<PasswordResetTokenPayload?> ConsumeTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the counter for a given rate-limit bucket (email or IP). Returns the new count.
    /// The bucket auto-expires after <paramref name="window"/>.
    /// </summary>
    Task<long> IncrementRateLimitAsync(string bucketKey, TimeSpan window, CancellationToken cancellationToken = default);
}

public sealed record PasswordResetTokenPayload(Guid TenantId, string Email);
