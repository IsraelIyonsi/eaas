using EaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EaaS.Infrastructure.Services;

/// <summary>
/// Redis-backed password reset token store. Tokens are stored with a TTL and deleted on consumption
/// (single-use). Rate-limit counters are INCR-based buckets that expire after the window.
/// </summary>
public sealed partial class RedisPasswordResetTokenStore : IPasswordResetTokenStore
{
    private const string TokenKeyPrefix = "pwreset:";
    private const string RateLimitKeyPrefix = "pwreset:rate:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPasswordResetTokenStore> _logger;

    public RedisPasswordResetTokenStore(IConnectionMultiplexer redis, ILogger<RedisPasswordResetTokenStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task StoreTokenAsync(string tokenHash, Guid tenantId, string email, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = TokenKey(tokenHash);
        // Value format: {tenantId}:{email}
        var value = $"{tenantId:D}:{email}";
        await db.StringSetAsync(key, value, ttl);
    }

    public async Task<PasswordResetTokenPayload?> ConsumeTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = TokenKey(tokenHash);
        // GETDEL is atomic: returns the value and deletes the key.
        var value = await db.StringGetDeleteAsync(key);
        if (!value.HasValue)
        {
            return null;
        }

        var raw = value.ToString();
        var sep = raw.IndexOf(':');
        if (sep <= 0 || sep >= raw.Length - 1)
        {
            LogMalformedToken(_logger, tokenHash);
            return null;
        }

        if (!Guid.TryParse(raw.AsSpan(0, sep), out var tenantId))
        {
            LogMalformedToken(_logger, tokenHash);
            return null;
        }

        var email = raw[(sep + 1)..];
        return new PasswordResetTokenPayload(tenantId, email);
    }

    public async Task<long> IncrementRateLimitAsync(string bucketKey, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = RateLimitKey(bucketKey);
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            // First hit in the bucket — set the expiry.
            await db.KeyExpireAsync(key, window);
        }
        return count;
    }

    private static string TokenKey(string tokenHash) => $"{TokenKeyPrefix}{tokenHash}";
    private static string RateLimitKey(string bucket) => $"{RateLimitKeyPrefix}{bucket}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password reset token payload malformed for hash prefix {TokenHashPrefix}")]
    private static partial void LogMalformedToken(ILogger logger, string tokenHashPrefix);
}
