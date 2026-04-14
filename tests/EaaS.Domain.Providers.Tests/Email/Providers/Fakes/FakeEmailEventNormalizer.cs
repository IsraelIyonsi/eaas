using System.Text;
using System.Text.Json;
using EaaS.Domain.Providers;

namespace EaaS.Domain.Providers.Tests.Email.Providers.Fakes;

/// <summary>
/// Trivial JSON normalizer used to validate the normalizer contract. Expects payloads like:
/// <c>{ "type": "delivered", "bounce_kind": "hard|soft", "messageId": "...", "recipient": "...", "occurredAt": "ISO" }</c>.
/// </summary>
public sealed class FakeEmailEventNormalizer : IEmailEventNormalizer
{
    public string ProviderKey => "fake";

    public Task<IReadOnlyList<ProviderEmailEvent>> NormalizeAsync(
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var empty = (IReadOnlyList<ProviderEmailEvent>)Array.Empty<ProviderEmailEvent>();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return Task.FromResult(empty);
        }

        using (doc)
        {
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

            var mapped = MapType(type, root);
            if (mapped is null)
            {
                return Task.FromResult(empty);
            }

            var evt = new ProviderEmailEvent(
                ProviderKey: ProviderKey,
                ProviderMessageId: root.TryGetProperty("messageId", out var msg) ? msg.GetString() ?? string.Empty : string.Empty,
                Type: mapped.Value,
                OccurredAt: root.TryGetProperty("occurredAt", out var ts) && ts.ValueKind == JsonValueKind.String
                    ? DateTimeOffset.Parse(ts.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                    : DateTimeOffset.UtcNow,
                Recipient: root.TryGetProperty("recipient", out var rcpt) ? rcpt.GetString() : null,
                DiagnosticCode: null,
                ProviderMetadata: new Dictionary<string, string>());

            return Task.FromResult((IReadOnlyList<ProviderEmailEvent>)new[] { evt });
        }
    }

    private static EmailEventType? MapType(string? type, JsonElement root) => type?.ToLowerInvariant() switch
    {
        "delivered" => EmailEventType.Delivered,
        "bounce" when root.TryGetProperty("bounce_kind", out var bk) && bk.GetString() == "hard"
            => EmailEventType.PermFailed,
        "bounce" when root.TryGetProperty("bounce_kind", out var bk) && bk.GetString() == "soft"
            => EmailEventType.TempFailed,
        "complaint" => EmailEventType.Complained,
        "click" => EmailEventType.Clicked,
        "open" => EmailEventType.Opened,
        "unsubscribe" => EmailEventType.Unsubscribed,
        _ => null,
    };

    public static ReadOnlyMemory<byte> BuildPayload(string type, string? bounceKind = null) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type,
            bounce_kind = bounceKind,
            messageId = "fake-msg-1",
            recipient = "user@example.com",
            occurredAt = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        }));
}
