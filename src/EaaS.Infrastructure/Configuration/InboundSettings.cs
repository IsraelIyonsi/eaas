namespace EaaS.Infrastructure.Configuration;

public sealed class InboundSettings
{
    public const string SectionName = "Inbound";

    public string S3BucketName { get; set; } = "eaas-inbound-emails";
    public string S3Region { get; set; } = "eu-west-1";
    public int MaxEmailSizeMb { get; set; } = 30;
    public int RetentionDays { get; set; } = 90;
    public bool Enabled { get; set; } = true;
}
