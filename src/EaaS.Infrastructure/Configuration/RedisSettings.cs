namespace EaaS.Infrastructure.Configuration;

public sealed class RedisSettings
{
    public const string SectionName = "ConnectionStrings";

    public string Redis { get; set; } = string.Empty;
}
