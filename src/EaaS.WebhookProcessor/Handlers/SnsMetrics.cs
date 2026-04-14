using System.Diagnostics.Metrics;

namespace EaaS.WebhookProcessor.Handlers;

/// <summary>
/// Prometheus-scraped counters for SNS webhook pipeline. Meter name is scraped by the
/// OpenTelemetry->Prometheus exporter registered in Program.cs (prometheus-net.AspNetCore).
/// </summary>
internal static class SnsMetrics
{
    public const string MeterName = "EaaS.WebhookProcessor.Sns";

    // Reason tags for signature_rejections_total.
    public const string ReasonBadHost = "bad_host";
    public const string ReasonBadCert = "bad_cert";
    public const string ReasonBadCertUrl = "bad_cert_url";
    public const string ReasonCertFetchFailed = "cert_fetch_failed";
    public const string ReasonSignatureMismatch = "signature_mismatch";
    public const string ReasonTimestampSkew = "timestamp_skew";
    public const string ReasonMissingField = "missing_field";

    // Result tags for sns_signature_verifications_total.
    public const string ResultSuccess = "success";
    public const string ResultRejected = "rejected";
    public const string ResultDisabled = "disabled";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> SignatureRejections =
        Meter.CreateCounter<long>("sns_signature_rejections_total");

    /// <summary>
    /// Outcome counter for every signature verification attempt. Tagged by <c>result</c>
    /// (success | rejected | disabled). Used to alert on silent failures
    /// (<c>absent_over_time</c>) and kill-switch flips (<c>result=disabled</c>).
    /// </summary>
    public static readonly Counter<long> SignatureVerifications =
        Meter.CreateCounter<long>("sns_signature_verifications_total");

    public static readonly Counter<long> DedupHits =
        Meter.CreateCounter<long>("sns_dedup_hits_total");

    public static readonly Counter<long> DedupUnavailable =
        Meter.CreateCounter<long>("sns_dedup_unavailable_total");

    public static readonly Counter<long> CertCacheMiss =
        Meter.CreateCounter<long>("sns_cert_cache_miss_total");

    public static readonly Counter<long> CertFetchFailures =
        Meter.CreateCounter<long>("sns_cert_fetch_failures_total");

    public static readonly Counter<long> SignatureVerificationDisabled =
        Meter.CreateCounter<long>("sns_signature_verification_disabled_total");

    public static void RejectForReason(string reason)
    {
        SignatureRejections.Add(1, new KeyValuePair<string, object?>("reason", reason));
        SignatureVerifications.Add(1, new KeyValuePair<string, object?>("result", ResultRejected));
    }

    public static void RecordVerificationSuccess()
        => SignatureVerifications.Add(1, new KeyValuePair<string, object?>("result", ResultSuccess));

    public static void RecordVerificationDisabled()
        => SignatureVerifications.Add(1, new KeyValuePair<string, object?>("result", ResultDisabled));
}
