namespace EaaS.Infrastructure.Configuration;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public int RequestsPerSecond { get; set; } = 100;
    public int BurstSize { get; set; } = 20;
}
