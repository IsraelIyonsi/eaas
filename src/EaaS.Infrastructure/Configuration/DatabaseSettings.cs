namespace EaaS.Infrastructure.Configuration;

public sealed class DatabaseSettings
{
    public const string SectionName = "ConnectionStrings";

    public string PostgreSQL { get; set; } = string.Empty;
}
