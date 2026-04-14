using System.Security.Cryptography;
using System.Text;
using EaaS.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

/// <summary>
/// Generates one-time HMAC-SHA256 password reset tokens and computes their storage hashes.
/// </summary>
public sealed class PasswordResetTokenService
{
    private readonly PasswordResetSettings _settings;

    public PasswordResetTokenService(IOptions<PasswordResetSettings> settings)
    {
        _settings = settings.Value;
        if (string.IsNullOrWhiteSpace(_settings.HmacSecret))
            throw new InvalidOperationException("PasswordReset:HmacSecret must be configured.");
    }

    /// <summary>
    /// Generates a new reset token. The payload is
    /// HMAC-SHA256({tenantId}:{email}:{issuedAtUnix}:{nonce}) keyed by the configured secret,
    /// encoded as URL-safe base64.
    /// </summary>
    public string GenerateToken(Guid tenantId, string email, DateTimeOffset issuedAt)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var payload = $"{tenantId:D}:{email.ToLowerInvariant()}:{issuedAt.ToUnixTimeSeconds()}:{nonce}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.HmacSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        // URL-safe base64: +/ -> -_, strip =
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Computes a storage-safe hash of the token (SHA-256 of the token bytes, hex-encoded).
    /// We never store the raw token in Redis — only its hash — so a Redis dump does not leak
    /// usable reset links.
    /// </summary>
    public static string HashTokenForStorage(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
