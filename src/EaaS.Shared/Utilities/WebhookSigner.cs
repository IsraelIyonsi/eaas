using System.Security.Cryptography;
using System.Text;

namespace EaaS.Shared.Utilities;

public static class WebhookSigner
{
    public static string ComputeSignature(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    public static void ApplyHeaders(
        HttpContent content,
        string? secret,
        string payload,
        string eventType,
        string deliveryId)
    {
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var signature = ComputeSignature(secret, payload);
            content.Headers.Add("X-EaaS-Signature", signature);
        }
        content.Headers.Add("X-EaaS-Event", eventType);
        content.Headers.Add("X-EaaS-Delivery-Id", deliveryId);
    }
}
