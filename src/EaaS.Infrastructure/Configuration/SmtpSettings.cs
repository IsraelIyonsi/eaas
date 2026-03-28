namespace EaaS.Infrastructure.Configuration;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public bool UseSsl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
}
