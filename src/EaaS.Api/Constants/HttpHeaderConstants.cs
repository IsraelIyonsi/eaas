namespace EaaS.Api.Constants;

public static class HttpHeaderConstants
{
    public const string AdminUserId = "X-Admin-User-Id";
    public const string AdminEmail = "X-Admin-Email";
    public const string AdminRole = "X-Admin-Role";
    public const string AdminProxyToken = "X-Admin-Proxy-Token";
    public const string TenantId = "X-Tenant-Id";
    public const string CorrelationId = "X-Correlation-Id";
    public const string RateLimitLimit = "X-RateLimit-Limit";
    public const string RateLimitRemaining = "X-RateLimit-Remaining";
    public const string RateLimitReset = "X-RateLimit-Reset";
    public const string RetryAfter = "Retry-After";
    public const string PaystackSignature = "X-Paystack-Signature";
    public const string StripeSignature = "Stripe-Signature";
    public const string Authorization = "Authorization";
    public const string BearerPrefix = "Bearer ";
}
