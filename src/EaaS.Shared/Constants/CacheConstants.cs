namespace EaaS.Shared.Constants;

public static class CacheConstants
{
    // Key prefixes
    public const string SuppressionPrefix = "suppression";
    public const string ApiKeyPrefix = "apikey";
    public const string IdempotencyPrefix = "idempotency";
    public const string TemplatePrefix = "template";
    public const string RateLimitPrefix = "ratelimit";

    // TTLs
    public static readonly TimeSpan ApiKeyCacheTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan TemplateCacheTtl = TimeSpan.FromMinutes(30);
}
