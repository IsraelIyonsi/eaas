using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EaaS.Application.Email.Providers;

namespace EaaS.Application.Tests.Email.Providers.Fakes;

/// <summary>HMAC-SHA256 reference verifier. Accepts headers "X-Signature" + "X-Timestamp".</summary>
public sealed class FakeWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    public const string SharedSecret = "fake-shared-secret";
    public static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(5);

    public string ProviderName => "fake";

    public Task<bool> VerifyAsync(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlyMemory<byte> rawBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!TryGet(headers, "X-Signature", out var signature) ||
            !TryGet(headers, "X-Timestamp", out var timestampStr))
        {
            return Task.FromResult(false);
        }

        if (!long.TryParse(timestampStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return Task.FromResult(false);
        }

        var ts = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (DateTimeOffset.UtcNow - ts > MaxSkew || ts - DateTimeOffset.UtcNow > MaxSkew)
        {
            return Task.FromResult(false);
        }

        var expected = Sign(timestampStr, rawBody.Span);
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(signature),
            Encoding.ASCII.GetBytes(expected));

        return Task.FromResult(ok);
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
