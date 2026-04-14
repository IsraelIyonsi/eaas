using System.Text.RegularExpressions;

namespace EaaS.WebhookProcessor.Handlers;

/// <summary>
/// Shared SNS validation utilities used by both SnsMessageHandler and SnsInboundHandler.
/// </summary>
internal static partial class SnsValidation
{
    // Anchored match: host MUST be exactly sns.<region>.amazonaws.com (no subdomains, no lookalikes).
    // Rejects sns.evil.amazonaws.com, sns.a.b.us-east-1.amazonaws.com, sns.us-east-1.amazonaws.com.evil.com.
    [GeneratedRegex(@"^sns\.[a-z0-9-]+\.amazonaws\.com$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SnsHostRegex();

    // Anchored match on the signing-cert path. AWS exposes these certs only at
    // /SimpleNotificationService-<hash>.pem. Suffix-only .pem checks let attackers slip
    // /attacker.pem or /SimpleNotificationService-abc.pem/../evil through — this regex forces
    // the full, literal shape and a non-empty hash segment.
    [GeneratedRegex(@"^/SimpleNotificationService-[A-Za-z0-9]+\.pem$", RegexOptions.CultureInvariant)]
    private static partial Regex SigningCertPathRegex();

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

        // Must be HTTPS, exact sns.<region>.amazonaws.com host, and the path must be literally
        // /SimpleNotificationService-<alnum>.pem (rejects traversal, lookalike filenames, empty hash).
        return uri.Scheme == Uri.UriSchemeHttps
               && SnsHostRegex().IsMatch(uri.Host)
               && SigningCertPathRegex().IsMatch(uri.AbsolutePath);
    }

    /// <summary>
    /// Validates that an SNS SubscribeURL points at the anchored sns.&lt;region&gt;.amazonaws.com
    /// host. Without this check an attacker-signed (or replayed) SubscriptionConfirmation
    /// could direct us at an internal IP — the SSRF-guarded HttpClient blocks the IP ranges,
    /// but URL-structure allowlisting gives defense in depth.
    /// </summary>
    internal static bool IsValidSubscribeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
               && SnsHostRegex().IsMatch(uri.Host);
    }
}
