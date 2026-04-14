using System.Security.Cryptography;
using System.Text;
using EaaS.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

/// <summary>
/// Generates and validates per-recipient unsubscribe tokens for RFC 8058
/// One-Click Unsubscribe and CAN-SPAM §7704 compliance.
/// Token = base64url( HMAC-SHA256(secret, tenantId|email|sentAt) )[..22]
/// The token is stateless — validation is performed by re-deriving from a
/// candidate payload. Since we do not store a mapping table of token->payload,
/// the unsubscribe endpoint receives the token plus a lookup identifier
/// embedded in the token payload. The short 22-char truncated digest is the
/// recipient-facing form used in URLs/headers; to make the endpoint lookup
/// tractable we pair the truncated digest with an opaque header payload.
/// </summary>
public sealed class ListUnsubscribeService
{
    private readonly byte[] _hmacKey;
    private readonly ListUnsubscribeSettings _settings;

    public ListUnsubscribeService(IOptions<ListUnsubscribeSettings> settings)
    {
        _settings = settings.Value;
        var secret = string.IsNullOrWhiteSpace(_settings.HmacSecret)
            ? "sendnex-unsubscribe-default-dev-secret-change-me"
            : _settings.HmacSecret;
        _hmacKey = Encoding.UTF8.GetBytes(secret);
    }

    /// <summary>
    /// Builds a stateless recipient token that encodes the tenant, recipient,
    /// and sentAt so the server can validate a one-click POST without a
    /// database lookup. Format: base64url( payload + "." + sig22 ) where
    /// payload = "{tenantId}:{emailLower}:{sentAtUnix}".
    /// </summary>
    public string GenerateToken(Guid tenantId, string recipientEmail, DateTime sentAt)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Recipient email required.", nameof(recipientEmail));

        var payload = $"{tenantId:N}:{recipientEmail.Trim().ToLowerInvariant()}:{new DateTimeOffset(sentAt, TimeSpan.Zero).ToUnixTimeSeconds()}";
        var sig22 = ComputeSig22(payload);
        var combined = payload + "." + sig22;
        return Base64UrlEncode(Encoding.UTF8.GetBytes(combined));
    }

    /// <summary>
    /// Validates the token and returns the decoded payload if the signature
    /// matches. Returns null for any malformed/tampered token.
    /// </summary>
    public UnsubscribeTokenData? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var bytes = Base64UrlDecode(token);
            var combined = Encoding.UTF8.GetString(bytes);
            var dot = combined.LastIndexOf('.');
            if (dot <= 0 || dot >= combined.Length - 1)
                return null;

            var payload = combined[..dot];
            var sig = combined[(dot + 1)..];
            var expected = ComputeSig22(payload);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(sig),
                    Encoding.UTF8.GetBytes(expected)))
                return null;

            var parts = payload.Split(':');
            if (parts.Length != 3)
                return null;

            if (!Guid.TryParseExact(parts[0], "N", out var tenantId))
                return null;

            if (!long.TryParse(parts[2], out var unix))
                return null;

            return new UnsubscribeTokenData(
                tenantId,
                parts[1],
                DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime);
        }
        catch
        {
            return null;
        }
    }

    private string ComputeSig22(string payload)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(hash)[..22];
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    public string MailtoUnsubscribe(string token)
        => $"mailto:unsubscribe+{token}@{_settings.MailtoHost}";

    public string HttpsUnsubscribe(string token)
        => $"{_settings.BaseUrl.TrimEnd('/')}/u/{token}";
}

public sealed record UnsubscribeTokenData(Guid TenantId, string RecipientEmail, DateTime SentAt);
