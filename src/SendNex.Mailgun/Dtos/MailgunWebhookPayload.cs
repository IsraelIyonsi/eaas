using System.Text.Json;
using System.Text.Json.Serialization;

namespace SendNex.Mailgun.Dtos;

/// <summary>
/// Signed event envelope posted by Mailgun to our webhook endpoint. The inner
/// <see cref="EventData"/> element is kept as a raw <see cref="JsonElement"/> —
/// the normalizer walks it directly to avoid leaking a deep DTO tree into the
/// Infrastructure project.
/// </summary>
public sealed record MailgunWebhookPayload(
    [property: JsonPropertyName("signature")] MailgunWebhookSignature? Signature,
    [property: JsonPropertyName("event-data")] JsonElement EventData);

public sealed record MailgunWebhookSignature(
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("token")] string? Token,
    [property: JsonPropertyName("signature")] string? Signature);
