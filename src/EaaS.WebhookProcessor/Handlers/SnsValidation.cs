namespace EaaS.WebhookProcessor.Handlers;

/// <summary>
/// Shared SNS validation utilities used by both SnsMessageHandler and SnsInboundHandler.
/// </summary>
internal static class SnsValidation
{
    /// <summary>
    /// Validates that an SNS signing certificate URL is a legitimate AWS endpoint.
    /// Prevents subscription confirmation to attacker-controlled URLs.
    /// </summary>
    internal static bool IsValidSigningCertUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Must be HTTPS from SNS cert endpoint with .pem extension
        return uri.Scheme == "https"
               && uri.Host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase)
               && uri.Host.StartsWith("sns.", StringComparison.OrdinalIgnoreCase)
               && uri.AbsolutePath.EndsWith(".pem", StringComparison.OrdinalIgnoreCase);
    }
}
