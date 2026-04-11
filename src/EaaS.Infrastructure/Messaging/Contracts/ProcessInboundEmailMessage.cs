namespace EaaS.Infrastructure.Messaging.Contracts;

public sealed record ProcessInboundEmailMessage
{
    public string S3BucketName { get; init; } = string.Empty;
    public string S3ObjectKey { get; init; } = string.Empty;
    public string SesMessageId { get; init; } = string.Empty;
    public string[] Recipients { get; init; } = Array.Empty<string>();
    public string? SpamVerdict { get; init; }
    public string? VirusVerdict { get; init; }
    public string? SpfVerdict { get; init; }
    public string? DkimVerdict { get; init; }
    public string? DmarcVerdict { get; init; }
}
