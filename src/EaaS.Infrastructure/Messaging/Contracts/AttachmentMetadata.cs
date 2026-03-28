using System.Text.Json.Serialization;

namespace EaaS.Infrastructure.Messaging.Contracts;

public sealed record AttachmentMetadata
{
    [JsonPropertyName("filename")]
    public string Filename { get; init; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    [JsonPropertyName("tempPath")]
    public string TempPath { get; init; } = string.Empty;
}
