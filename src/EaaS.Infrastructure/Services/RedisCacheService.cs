using EaaS.Domain.Interfaces;
using EaaS.Shared.Constants;
using EaaS.Shared.Utilities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EaaS.Infrastructure.Services;

public sealed partial class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly TimeSpan ApiKeyCacheTtl = CacheConstants.ApiKeyCacheTtl;

    // Lua script for sliding window rate limiting
    private const string RateLimitLuaScript = @"
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local max_requests = tonumber(ARGV[3])

        -- Remove expired entries
        redis.call('ZREMRANGEBYSCORE', key, 0, now - window)

        -- Count current entries
        local count = redis.call('ZCARD', key)

        if count < max_requests then
            -- Add current request
            redis.call('ZADD', key, now, now .. '-' .. math.random(1000000))
            redis.call('EXPIRE', key, math.ceil(window / 1000))
            return 1
        else
            return 0
        end
    ";

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> IsEmailSuppressedAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = CacheKeys.Suppression(tenantId, emailAddress);
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            LogSuppressionCheckFailed(_logger, ex, emailAddress);
            return false;
        }
    }

    public async Task AddToSuppressionCacheAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = CacheKeys.Suppression(tenantId, emailAddress);
            await db.StringSetAsync(key, "1");
        }
        catch (Exception ex)
        {
            LogSuppressionAddFailed(_logger, ex, emailAddress);
        }
    }

    public async Task RemoveFromSuppressionCacheAsync(Guid tenantId, string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = CacheKeys.Suppression(tenantId, emailAddress);
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            LogSuppressionRemoveFailed(_logger, ex, emailAddress);
        }
    }

    public async Task<bool> CheckRateLimitAsync(string key, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var rateLimitKey = CacheKeys.RateLimit(key);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var result = await db.ScriptEvaluateAsync(
                RateLimitLuaScript,
                new RedisKey[] { rateLimitKey },
                new RedisValue[] { now, (long)window.TotalMilliseconds, maxRequests });

            return (int)result == 1;
        }
        catch (Exception ex)
        {
            LogRateLimitCheckFailed(_logger, ex, key);
            return true; // Fail open
        }
    }

    public async Task<string?> GetApiKeyCacheAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.ApiKey(keyHash);
            var value = await db.StringGetAsync(cacheKey);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            LogApiKeyGetFailed(_logger, ex);
            return null;
        }
    }

    public async Task SetApiKeyCacheAsync(string keyHash, string serializedApiKey, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.ApiKey(keyHash);
            await db.StringSetAsync(cacheKey, serializedApiKey, ttl ?? ApiKeyCacheTtl);
        }
        catch (Exception ex)
        {
            LogApiKeySetFailed(_logger, ex);
        }
    }

    public async Task InvalidateApiKeyCacheAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.ApiKey(keyHash);
            await db.KeyDeleteAsync(cacheKey);
        }
        catch (Exception ex)
        {
            LogApiKeyInvalidateFailed(_logger, ex);
        }
    }

    private static readonly TimeSpan IdempotencyTtl = CacheConstants.IdempotencyTtl;

    public async Task<string?> GetIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.Idempotency(tenantId, key);
            var value = await db.StringGetAsync(cacheKey);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            LogIdempotencyGetFailed(_logger, ex, key);
            return null;
        }
    }

    public async Task SetIdempotencyKeyAsync(Guid tenantId, string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.Idempotency(tenantId, key);
            await db.StringSetAsync(cacheKey, value, IdempotencyTtl);
        }
        catch (Exception ex)
        {
            LogIdempotencySetFailed(_logger, ex, key);
        }
    }

    private static readonly TimeSpan TemplateCacheTtl = CacheConstants.TemplateCacheTtl;

    public async Task<string?> GetTemplateCacheAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.Template(templateId);
            var value = await db.StringGetAsync(cacheKey);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            LogTemplateGetFailed(_logger, ex, templateId);
            return null;
        }
    }

    public async Task SetTemplateCacheAsync(Guid templateId, string serializedTemplate, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.Template(templateId);
            await db.StringSetAsync(cacheKey, serializedTemplate, TemplateCacheTtl);
        }
        catch (Exception ex)
        {
            LogTemplateSetFailed(_logger, ex, templateId);
        }
    }

    public async Task InvalidateTemplateCacheAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheKey = CacheKeys.Template(templateId);
            await db.KeyDeleteAsync(cacheKey);
        }
        catch (Exception ex)
        {
            LogTemplateInvalidateFailed(_logger, ex, templateId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to check suppression cache for {Email}, falling back to database")]
    private static partial void LogSuppressionCheckFailed(ILogger logger, Exception ex, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to add {Email} to suppression cache")]
    private static partial void LogSuppressionAddFailed(ILogger logger, Exception ex, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to remove {Email} from suppression cache")]
    private static partial void LogSuppressionRemoveFailed(ILogger logger, Exception ex, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit check failed for key {Key}, allowing request")]
    private static partial void LogRateLimitCheckFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get API key from cache")]
    private static partial void LogApiKeyGetFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to cache API key")]
    private static partial void LogApiKeySetFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to invalidate API key cache")]
    private static partial void LogApiKeyInvalidateFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get idempotency key {Key}")]
    private static partial void LogIdempotencyGetFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to set idempotency key {Key}")]
    private static partial void LogIdempotencySetFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get template cache for {TemplateId}")]
    private static partial void LogTemplateGetFailed(ILogger logger, Exception ex, Guid templateId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to set template cache for {TemplateId}")]
    private static partial void LogTemplateSetFailed(ILogger logger, Exception ex, Guid templateId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to invalidate template cache for {TemplateId}")]
    private static partial void LogTemplateInvalidateFailed(ILogger logger, Exception ex, Guid templateId);
}
