using System.Text.Json.Serialization;

namespace SendNex.Mailgun.Dtos;

/// <summary>Successful response envelope from <c>POST /v3/{domain}/messages</c>.</summary>
public sealed record MailgunSendResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("message")] string? Message);
