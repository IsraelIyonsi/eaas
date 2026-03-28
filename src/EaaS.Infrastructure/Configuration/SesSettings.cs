namespace EaaS.Infrastructure.Configuration;

public sealed class SesSettings
{
    public const string SectionName = "Ses";

    public string Region { get; set; } = "eu-west-1";
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public int MaxSendRate { get; set; } = 14;
}
