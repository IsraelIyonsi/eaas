namespace EaaS.Shared.Constants;

public static class RateLimitConstants
{
    public const int DefaultMaxRequestsPerMinute = 100;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);
}
