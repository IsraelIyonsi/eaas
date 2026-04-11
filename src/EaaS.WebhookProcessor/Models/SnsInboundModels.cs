using System.Text.Json.Serialization;

namespace EaaS.WebhookProcessor.Models;

public sealed class SesInboundNotification
{
    [JsonPropertyName("notificationType")]
    public string NotificationType { get; set; } = string.Empty;

    [JsonPropertyName("receipt")]
    public SesInboundReceipt Receipt { get; set; } = new();

    [JsonPropertyName("mail")]
    public SesInboundMail Mail { get; set; } = new();
}

public sealed class SesInboundReceipt
{
    [JsonPropertyName("action")]
    public SesInboundAction Action { get; set; } = new();

    [JsonPropertyName("spamVerdict")]
    public SesVerdict SpamVerdict { get; set; } = new();

    [JsonPropertyName("virusVerdict")]
    public SesVerdict VirusVerdict { get; set; } = new();

    [JsonPropertyName("spfVerdict")]
    public SesVerdict SpfVerdict { get; set; } = new();

    [JsonPropertyName("dkimVerdict")]
    public SesVerdict DkimVerdict { get; set; } = new();

    [JsonPropertyName("dmarcVerdict")]
    public SesVerdict DmarcVerdict { get; set; } = new();
}

public sealed class SesInboundAction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("bucketName")]
    public string BucketName { get; set; } = string.Empty;

    [JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;
}

public sealed class SesVerdict
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed class SesInboundMail
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("destination")]
    public List<string> Destination { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}
