namespace EaaS.Shared.Utilities;

/// <summary>
/// Configuration surface for <see cref="SsrfGuardService"/>. Bound from the
/// <c>SsrfGuard</c> config section at startup. Every knob here is an
/// operational escape hatch: we want ops to be able to loosen specific
/// constraints without a redeploy, without widening the full attack surface.
/// </summary>
public sealed class SsrfGuardOptions
{
    /// <summary>Config section name (<c>SsrfGuard</c>).</summary>
    public const string SectionName = "SsrfGuard";

    /// <summary>
    /// Master kill-switch. Defaults to <c>true</c>. When <c>false</c>, all
    /// SSRF validation is bypassed and an <c>Error</c> log + the
    /// <c>webhook_ssrf_guard_disabled_total</c> counter fire once per request.
    /// Timeouts and redirect caps still apply — this only disables the
    /// IP/host allow/deny logic.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Additional CIDR ranges that should be treated as public. Useful when a
    /// customer legitimately needs CGNAT (<c>100.64.0.0/10</c>) or a specific
    /// corporate egress IP re-allowed. Format: "a.b.c.d/len" or "::/len".
    /// </summary>
    public List<string> ExtraAllowedCidrs { get; set; } = new();

    /// <summary>
    /// Hostnames that bypass the syntactic blocked-suffix check (e.g.
    /// <c>staging-tunnel.ngrok.io</c> in dev). Exact match, case-insensitive.
    /// The DNS/IP check still runs.
    /// </summary>
    public List<string> AllowedHostOverrides { get; set; } = new();
}
