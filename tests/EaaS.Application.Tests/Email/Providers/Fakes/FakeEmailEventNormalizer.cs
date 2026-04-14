using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EaaS.Application.Email.Providers;

namespace EaaS.Application.Tests.Email.Providers.Fakes;

/// <summary>
/// Trivial JSON normalizer used to validate the normalizer contract. Expects payloads like:
/// <c>{ "type": "delivered", "bounce_kind": "hard|soft", "messageId": "...", "recipient": "...", "occurredAt": "ISO" }</c>.
/// </summary>
public sealed class FakeEmailEventNormalizer : IEmailEventNormalizer
{
    public string ProviderName => "fake";

    public async IAsyncEnumerable<EmailEvent> NormalizeAsync(
        ReadOnlyMemory<byte> rawBody,
        IReadOnlyDictionary<string, string> headers,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

            var mapped = MapType(type, root);
            if (mapped is null)
            {
                yield break;
            }

            yield return new EmailEvent
            {
                ProviderName = ProviderName,
                ProviderMessageId = root.TryGetProperty("messageId", out var msg) ? msg.GetString() ?? "" : "",
                Type = mapped.Value,
                OccurredAt = root.TryGetProperty("occurredAt", out var ts) && ts.ValueKind == JsonValueKind.String
                    ? DateTimeOffset.Parse(ts.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                    : DateTimeOffset.UtcNow,
                Recipient = root.TryGetProperty("recipient", out var rcpt) ? rcpt.GetString() : null,
                RawPayload = rawBody,
            };
        }
    }

    private static EmailEventType? MapType(string? type, JsonElement root) => type?.ToLowerInvariant() switch
    {
        "delivered" => EmailEventType.Delivered,
        "bounce" when root.TryGetProperty("bounce_kind", out var bk) && bk.GetString() == "hard"
            => EmailEventType.PermanentFailure,
        "bounce" when root.TryGetProperty("bounce_kind", out var bk) && bk.GetString() == "soft"
            => EmailEventType.TemporaryFailure,
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
