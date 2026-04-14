namespace EaaS.WebhookProcessor.Configuration;

/// <summary>
/// Bindable options for the SNS webhook pipeline (signature verification, replay dedup, body caps,
/// cert cache tuning, negative-cache TTL, and an emergency kill switch).
/// Bound from configuration section <c>Sns</c>.
/// </summary>
public sealed class SnsWebhookOptions
{
    public const string SectionName = "Sns";

    /// <summary>
    /// Master kill switch. When false, signature verification is skipped. Intended for incident
    /// response ONLY. Each skipped request emits a loud error log and increments
    /// <c>sns_signature_verification_disabled_total</c>.
    /// </summary>
    public bool SignatureVerificationEnabled { get; set; } = true;

    /// <summary>Bounded LRU capacity for the signing-cert cache.</summary>
    public int CacheCapacity { get; set; } = 32;

    /// <summary>How long a fetched signing cert stays in the positive cache.</summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Max absolute skew between SNS Timestamp and server clock before we reject.
    /// AWS SNS timestamps are fresh (emitted at send time); 15m is plenty of headroom for
    /// our own clock drift while keeping the replay window tight. 4x shrink from the previous 1h.
    /// </summary>
    public TimeSpan MaxClockSkew { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>MessageId replay-dedup TTL (Redis NX+EX). Must outlive SNS retry window (~1h).</summary>
    public TimeSpan ReplayDedupTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Hard cap on inbound SNS webhook body bytes. AWS SNS payload ceiling is 150 KB.
    /// </summary>
    public long MaxBodyBytes { get; set; } = 150_000;

    /// <summary>
    /// How long to cache a cert-fetch failure before we'll attempt the outbound GET again.
    /// Short window keeps us resilient to transient blips without hammering AWS on persistent errors.
    /// </summary>
    public TimeSpan NegativeCacheTtl { get; set; } = TimeSpan.FromMinutes(1);
}
