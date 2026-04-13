namespace EaaS.Shared.Constants;

public static class RateLimitConstants
{
    public const int DefaultMaxRequestsPerMinute = 100;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    // Auth login rate limiting (ASP.NET built-in fixed window)
    public const string AuthLoginPolicy = "AuthLogin";
    public static readonly TimeSpan AuthLoginWindow = TimeSpan.FromMinutes(1);
    public const int AuthLoginPermitLimit = 5;
    public const string AuthLoginRetryAfterSeconds = "60";
}
