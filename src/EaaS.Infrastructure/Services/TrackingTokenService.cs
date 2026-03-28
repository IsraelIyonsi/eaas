using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

public sealed partial class TrackingTokenService : ITrackingTokenService
{
    private readonly byte[] _hmacKey;
    private readonly ILogger<TrackingTokenService> _logger;

    public TrackingTokenService(IOptions<TrackingSettings> settings, ILogger<TrackingTokenService> logger)
    {
        _hmacKey = Encoding.UTF8.GetBytes(settings.Value.HmacSecret);
        _logger = logger;
    }

    public string GenerateToken(Guid emailId, string eventType, string? originalUrl = null)
    {
        var payload = new TrackingTokenPayload(emailId, eventType, originalUrl);
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var signature = ComputeHmac(payloadBytes);

        // Combine: [4 bytes sig length][signature][payload]
        var combined = new byte[4 + signature.Length + payloadBytes.Length];
        BitConverter.GetBytes(signature.Length).CopyTo(combined, 0);
        signature.CopyTo(combined, 4);
        payloadBytes.CopyTo(combined, 4 + signature.Length);

        var token = Convert.ToBase64String(combined)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return token;
    }

    public TrackingTokenData? ValidateToken(string token)
    {
        try
        {
            // Restore base64 padding
            var base64 = token.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var combined = Convert.FromBase64String(base64);

            if (combined.Length < 4)
                return null;

            var sigLength = BitConverter.ToInt32(combined, 0);
            if (combined.Length < 4 + sigLength)
                return null;

            var signature = combined.AsSpan(4, sigLength);
            var payloadBytes = combined.AsSpan(4 + sigLength);

            var expectedSignature = ComputeHmac(payloadBytes.ToArray());
            if (!CryptographicOperations.FixedTimeEquals(signature, expectedSignature))
            {
                LogInvalidSignature(_logger);
                return null;
            }

            var payload = JsonSerializer.Deserialize<TrackingTokenPayload>(payloadBytes);
            if (payload is null)
                return null;

            return new TrackingTokenData(payload.EmailId, payload.EventType, payload.OriginalUrl);
        }
        catch (Exception ex)
        {
            LogTokenValidationFailed(_logger, ex);
            return null;
        }
    }

    private byte[] ComputeHmac(byte[] data)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        return hmac.ComputeHash(data);
    }

    private sealed record TrackingTokenPayload(Guid EmailId, string EventType, string? OriginalUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tracking token has invalid HMAC signature")]
    private static partial void LogInvalidSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to validate tracking token")]
    private static partial void LogTokenValidationFailed(ILogger logger, Exception ex);
}
