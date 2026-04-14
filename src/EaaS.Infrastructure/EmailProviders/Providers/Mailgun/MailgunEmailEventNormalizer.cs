using System.Globalization;
using System.Text.Json;
using EaaS.Domain.Providers;
using Microsoft.Extensions.Logging;
using SendNex.Mailgun;

namespace EaaS.Infrastructure.EmailProviders.Providers.Mailgun;

/// <summary>
/// Maps Mailgun webhook payloads to the canonical <see cref="ProviderEmailEvent"/>.
/// The <c>failed</c> event bifurcates by <c>severity</c> (<c>permanent</c> -&gt;
/// <see cref="EmailEventType.Bounced"/>, <c>temporary</c> -&gt;
/// <see cref="EmailEventType.TempFailed"/>) per §7 of the architecture doc.
/// Tenant attribution is pulled from <c>event-data.user-variables.tenant_id</c>.
/// </summary>
public sealed partial class MailgunEmailEventNormalizer : IEmailEventNormalizer
{
    private static readonly IReadOnlyList<ProviderEmailEvent> Empty =
        Array.Empty<ProviderEmailEvent>();

    private readonly ILogger<MailgunEmailEventNormalizer> _logger;

    public MailgunEmailEventNormalizer(ILogger<MailgunEmailEventNormalizer> logger)
    {
        _logger = logger;
    }

    public string ProviderKey => MailgunProviderKey.Value;

    public Task<IReadOnlyList<ProviderEmailEvent>> NormalizeAsync(
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty(MailgunConstants.Webhook.EventData, out var eventData) ||
                eventData.ValueKind != JsonValueKind.Object)
            {
                LogMissingEventData(_logger);
                return Task.FromResult(Empty);
            }

            var evt = Normalize(eventData);
            IReadOnlyList<ProviderEmailEvent> result =
                evt is null ? Empty : new[] { evt };
            return Task.FromResult(result);
        }
        catch (JsonException ex)
        {
            LogMalformedPayload(_logger, ex);
            return Task.FromResult(Empty);
        }
    }

    private ProviderEmailEvent? Normalize(JsonElement eventData)
    {
        var eventName = eventData.TryGetProperty(MailgunConstants.Webhook.Event, out var e)
            ? e.GetString() : null;
        if (string.IsNullOrEmpty(eventName))
        {
            LogMissingEventName(_logger);
            return null;
        }

        var severity = eventData.TryGetProperty(MailgunConstants.Webhook.Severity, out var s)
            ? s.GetString() : null;

        var mapped = MapEvent(eventName, severity);
        if (mapped is null)
        {
            LogUnknownEvent(_logger, eventName);
            return null;
        }

        var messageId = ExtractMessageId(eventData);
        if (string.IsNullOrEmpty(messageId))
        {
            LogMissingMessageId(_logger, eventName);
            return null;
        }

        var occurredAt = ExtractOccurredAt(eventData);
        var recipient = eventData.TryGetProperty(MailgunConstants.Webhook.Recipient, out var r)
            ? r.GetString() : null;
        var diagnostic = ExtractDiagnostic(eventData);

        var metadata = BuildMetadata(eventData, eventName, severity);

        return new ProviderEmailEvent(
            ProviderKey: MailgunProviderKey.Value,
            ProviderMessageId: messageId,
            Type: mapped.Value,
            OccurredAt: occurredAt,
            Recipient: recipient,
            DiagnosticCode: diagnostic,
            ProviderMetadata: metadata);
    }

    private static EmailEventType? MapEvent(string eventName, string? severity) => eventName switch
    {
        MailgunConstants.Events.Accepted => EmailEventType.Accepted,
        MailgunConstants.Events.Delivered => EmailEventType.Delivered,
        MailgunConstants.Events.Opened => EmailEventType.Opened,
        MailgunConstants.Events.Clicked => EmailEventType.Clicked,
        MailgunConstants.Events.Unsubscribed => EmailEventType.Unsubscribed,
        MailgunConstants.Events.Complained => EmailEventType.Complained,
        MailgunConstants.Events.Stored => EmailEventType.Stored,
        MailgunConstants.Events.Rejected => EmailEventType.PermFailed,
        MailgunConstants.Events.Failed => severity switch
        {
            MailgunConstants.Severity.Permanent => EmailEventType.Bounced,
            MailgunConstants.Severity.Temporary => EmailEventType.TempFailed,
            _ => EmailEventType.Bounced
        },
        _ => null
    };

    private static string? ExtractMessageId(JsonElement eventData)
    {
        if (eventData.TryGetProperty(MailgunConstants.Webhook.Message, out var msg) &&
            msg.ValueKind == JsonValueKind.Object &&
            msg.TryGetProperty(MailgunConstants.Webhook.Headers, out var h) &&
            h.ValueKind == JsonValueKind.Object &&
            h.TryGetProperty(MailgunConstants.Webhook.MessageId, out var id))
        {
            return id.GetString();
        }
        return null;
    }

    private static DateTimeOffset ExtractOccurredAt(JsonElement eventData)
    {
        if (!eventData.TryGetProperty(MailgunConstants.Webhook.Timestamp, out var ts))
            return DateTimeOffset.UtcNow;

        return ts.ValueKind switch
        {
            JsonValueKind.Number when ts.TryGetDouble(out var d) =>
                DateTimeOffset.FromUnixTimeMilliseconds((long)(d * 1000)),
            JsonValueKind.String when long.TryParse(ts.GetString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var l) =>
                DateTimeOffset.FromUnixTimeSeconds(l),
            _ => DateTimeOffset.UtcNow
        };
    }

    private static string? ExtractDiagnostic(JsonElement eventData)
    {
        if (eventData.TryGetProperty(MailgunConstants.Webhook.DeliveryStatus, out var ds) &&
            ds.ValueKind == JsonValueKind.Object &&
            ds.TryGetProperty(MailgunConstants.Webhook.Code, out var code))
        {
            return code.ValueKind switch
            {
                JsonValueKind.Number => code.GetRawText(),
                JsonValueKind.String => code.GetString(),
                _ => null
            };
        }
        if (eventData.TryGetProperty(MailgunConstants.Webhook.Reason, out var reason))
            return reason.GetString();
        return null;
    }

    private static Dictionary<string, string> BuildMetadata(
        JsonElement eventData,
        string eventName,
        string? severity)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MailgunConstants.Webhook.Event] = eventName
        };
        if (!string.IsNullOrEmpty(severity))
            dict[MailgunConstants.Webhook.Severity] = severity;

        if (eventData.TryGetProperty(MailgunConstants.Webhook.UserVariables, out var vars) &&
            vars.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in vars.EnumerateObject())
            {
                var val = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False =>
                        prop.Value.GetRawText(),
                    _ => null
                };
                if (val is not null)
                    dict[MailgunConstants.Webhook.UserVariables + "." + prop.Name] = val;
            }
        }

        return dict;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mailgun webhook payload missing event-data")]
    private static partial void LogMissingEventData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mailgun webhook missing event name")]
    private static partial void LogMissingEventName(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown Mailgun event type: {EventName}")]
    private static partial void LogUnknownEvent(ILogger logger, string eventName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mailgun webhook {EventName} missing Message-Id header")]
    private static partial void LogMissingMessageId(ILogger logger, string eventName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Malformed Mailgun webhook payload")]
    private static partial void LogMalformedPayload(ILogger logger, Exception ex);
}
