using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;

namespace EaaS.Api.Authentication;

public class AdminSessionAuthSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// HMAC-SHA256 secret used to verify admin session cookies.
    /// Must match the SESSION_SECRET used by the dashboard.
    /// Marked [JsonIgnore] and redacted in ToString() so the secret cannot leak
    /// via diagnostics, logs, or error payloads that serialise options.
    /// </summary>
    [JsonIgnore]
    public string SessionSecret { get; set; } = string.Empty;

    /// <summary>
    /// When true (default), admin requests via the proxy fallback MUST include a valid
    /// HMAC-signed X-Admin-Proxy-Token. When false, unsigned X-Admin-User-Id headers are
    /// accepted with a warning log — this is the legacy BROKEN behaviour and exists only
    /// to support zero-downtime rollout of the signed-token contract. Production MUST flip
    /// this to true immediately after the dashboard ships signed tokens.
    /// </summary>
    public bool RequireProxyToken { get; set; } = true;

    /// <summary>
    /// Optional hard cut-over timestamp. If set, the legacy unsigned-header escape hatch
    /// (<see cref="RequireProxyToken"/>=false) is ONLY honoured while
    /// <c>DateTimeOffset.UtcNow &lt; EnforceAfter</c>. After that instant, unsigned headers
    /// are rejected regardless of the <see cref="RequireProxyToken"/> flag — guaranteeing
    /// the rollout window is time-bound and cannot be left open indefinitely.
    /// </summary>
    public DateTimeOffset? EnforceAfter { get; set; }

    /// <summary>
    /// Redacted string form. The default <see cref="object.ToString"/> uses reflection-style
    /// rendering in some tooling; we deliberately suppress the secret so it never surfaces
    /// in logs, Problem Details, or dump files.
    /// </summary>
    public override string ToString()
        => $"AdminSessionAuthSchemeOptions {{ SessionSecret=***, RequireProxyToken={RequireProxyToken}, EnforceAfter={EnforceAfter?.ToString("O") ?? "null"} }}";
}
