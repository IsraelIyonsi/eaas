using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EaaS.Domain.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendNex.Mailgun;

namespace EaaS.Infrastructure.EmailProviders.Providers.Mailgun;

/// <summary>
/// HMAC-SHA256 verifier for Mailgun webhook payloads. The canonical message is
/// <c>timestamp || token</c>, signed with the account's <c>WebhookSigningKey</c>.
/// Requests with timestamps outside the <see cref="MailgunConstants.Webhook.ReplayTolerance"/>
/// window are rejected before the HMAC check — cheap defence against replay.
/// </summary>
public sealed partial class MailgunWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly MailgunOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MailgunWebhookSignatureVerifier> _logger;

    public MailgunWebhookSignatureVerifier(
        IOptions<MailgunOptions> options,
        TimeProvider timeProvider,
        ILogger<MailgunWebhookSignatureVerifier> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public string ProviderKey => MailgunProviderKey.Value;

    /// <summary>Name of the timestamp field as carried in the Mailgun payload.</summary>
    public static string GetTimestampHeader() => MailgunConstants.Webhook.Timestamp;

    public Task<WebhookVerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);

        string? timestamp;
        string? token;
        string? signature;

        try
        {
            (timestamp, token, signature) = ExtractSignature(payload);
        }
        catch (JsonException ex)
        {
            LogMalformed(_logger, ex);
            return Task.FromResult(new WebhookVerificationResult(false, "Malformed webhook payload (invalid JSON)."));
        }

        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(signature))
            return Task.FromResult(new WebhookVerificationResult(false, "Missing timestamp, token, or signature."));

        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts))
            return Task.FromResult(new WebhookVerificationResult(false, "Timestamp is not an integer."));

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        var skew = _timeProvider.GetUtcNow() - eventTime;
        if (skew.Duration() > MailgunConstants.Webhook.ReplayTolerance)
            return Task.FromResult(new WebhookVerificationResult(false, "Timestamp outside replay tolerance."));

        var expected = ComputeHmacHex(_options.WebhookSigningKey, timestamp + token);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(signature);

        if (expectedBytes.Length != actualBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            return Task.FromResult(new WebhookVerificationResult(false, "Signature mismatch."));
        }

        return Task.FromResult(new WebhookVerificationResult(true, null));
    }

    private static (string? timestamp, string? token, string? signature) ExtractSignature(
        ReadOnlyMemory<byte> payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(MailgunConstants.Webhook.SignatureObject, out var sig) ||
            sig.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        return (
            sig.TryGetProperty(MailgunConstants.Webhook.Timestamp, out var t) ? t.GetString() : null,
            sig.TryGetProperty(MailgunConstants.Webhook.Token, out var k) ? k.GetString() : null,
            sig.TryGetProperty(MailgunConstants.Webhook.Signature, out var s) ? s.GetString() : null
        );
    }

    private static string ComputeHmacHex(string key, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hash = HMACSHA256.HashData(keyBytes, messageBytes);
        return Convert.ToHexStringLower(hash);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Malformed Mailgun webhook payload")]
    private static partial void LogMalformed(ILogger logger, Exception ex);
}
