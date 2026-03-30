using EaaS.Shared.Constants;

namespace EaaS.Shared.Utilities;

public static class CacheKeys
{
    public static string Suppression(Guid tenantId, string email)
        => $"{CacheConstants.SuppressionPrefix}:{tenantId}:{email.ToLowerInvariant()}";

    public static string ApiKey(string keyHash)
        => $"{CacheConstants.ApiKeyPrefix}:{keyHash}";

    public static string Idempotency(Guid tenantId, string key)
        => $"{CacheConstants.IdempotencyPrefix}:{tenantId}:{key}";

    public static string Template(Guid templateId)
        => $"{CacheConstants.TemplatePrefix}:{templateId}";

    public static string RateLimit(string key)
        => $"{CacheConstants.RateLimitPrefix}:{key}";
}
