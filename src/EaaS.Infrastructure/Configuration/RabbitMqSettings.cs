namespace EaaS.Infrastructure.Configuration;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "eaas";
    public int PrefetchCount { get; set; } = 10;
}
