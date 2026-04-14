using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EaaS.Domain.Providers;

namespace EaaS.Domain.Providers.Tests.Email.Providers.Fakes;

/// <summary>HMAC-SHA256 reference verifier. Accepts headers "X-Signature" + "X-Timestamp".</summary>
public sealed class FakeWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    public const string SharedSecret = "fake-shared-secret";
    public const string TimestampHeader = "X-Timestamp";
    public const string SignatureHeader = "X-Signature";
    public static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(5);

    public string ProviderKey => "fake";

    public Task<WebhookVerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!TryGet(headers, SignatureHeader, out var signature) ||
            !TryGet(headers, TimestampHeader, out var timestampStr))
        {
            return Task.FromResult(new WebhookVerificationResult(false, "missing headers"));
        }

        if (!long.TryParse(timestampStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return Task.FromResult(new WebhookVerificationResult(false, "invalid timestamp"));
        }

        var ts = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (DateTimeOffset.UtcNow - ts > MaxSkew || ts - DateTimeOffset.UtcNow > MaxSkew)
        {
            return Task.FromResult(new WebhookVerificationResult(false, "timestamp outside skew window"));
        }

        var expected = Sign(timestampStr, payload.Span);
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(signature),
            Encoding.ASCII.GetBytes(expected));

        return Task.FromResult(ok
            ? new WebhookVerificationResult(true, null)
            : new WebhookVerificationResult(false, "signature mismatch"));
    }

    public static string Sign(string timestamp, ReadOnlySpan<byte> body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        hmac.TransformBlock(Encoding.UTF8.GetBytes(timestamp + "."), 0, timestamp.Length + 1, null, 0);
        var bodyArr = body.ToArray();
        hmac.TransformFinalBlock(bodyArr, 0, bodyArr.Length);
        return Convert.ToHexString(hmac.Hash!);
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> headers, string key, out string value)
    {
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }
}
