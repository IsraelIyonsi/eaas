using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EaaS.WebhookProcessor.Configuration;
using EaaS.WebhookProcessor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EaaS.WebhookProcessor.Handlers;

/// <summary>
/// Verifies AWS SNS message signatures per the AWS specification.
/// Supports SignatureVersion 1 (SHA256withRSA) and SignatureVersion 2 (same algorithm, newer topics).
/// Caches fetched signing certificates by URL and coalesces concurrent fetches.
/// </summary>
public sealed partial class SnsSignatureVerifier
{
    // Fallback defaults used when options aren't injected (test helpers only).
    internal const int CacheCapacity = 32;
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);
    internal static readonly TimeSpan MaxClockSkew = TimeSpan.FromHours(1);
    private static readonly TimeSpan DefaultNegativeCacheTtl = TimeSpan.FromMinutes(1);

    // LRU via Dictionary+LinkedList. All access through _cacheLock.
    private static readonly Dictionary<string, LinkedListNode<CachedCert>> CacheIndex =
        new(StringComparer.Ordinal);
    private static readonly LinkedList<CachedCert> CacheOrder = new();
    private static readonly Lock CacheLock = new();

    // Coalesces concurrent cert fetches for the same URL so only one outbound HTTP call happens
    // when N requests miss the positive cache simultaneously.
    private static readonly ConcurrentDictionary<string, Lazy<Task<X509Certificate2>>> InFlightFetches =
        new(StringComparer.Ordinal);

    // Negative cache: remembers recent fetch failures so we fail fast instead of re-hitting AWS
    // during an ongoing outage. Short TTL (default 60s) keeps us responsive to recovery.
    private static readonly ConcurrentDictionary<string, NegativeCacheEntry> NegativeCache =
        new(StringComparer.Ordinal);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SnsSignatureVerifier> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<SnsWebhookOptions> _options;

    public SnsSignatureVerifier(
        IHttpClientFactory httpClientFactory,
        ILogger<SnsSignatureVerifier> logger,
        TimeProvider? timeProvider = null,
        IOptionsMonitor<SnsWebhookOptions>? options = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        // IOptionsMonitor.CurrentValue re-reads on every access so config hot-reload (kill switch,
        // skew, TTLs) takes effect without a process restart — required for zero-downtime ops.
        _options = options ?? new StaticOptionsMonitor(new SnsWebhookOptions());
    }

    /// <summary>
    /// Master kill switch. When false the webhook handlers MUST skip calling <see cref="VerifyAsync"/>,
    /// log a loud error, and increment <c>sns_signature_verification_disabled_total</c>.
    /// Read at every access (not cached) so IOptionsMonitor hot-reload flips apply without restart.
    /// </summary>
    public bool SignatureVerificationEnabled => _options.CurrentValue.SignatureVerificationEnabled;

    /// <summary>Exposed so handlers share a single source of truth for the dedup TTL.</summary>
    public TimeSpan ReplayDedupTtl => _options.CurrentValue.ReplayDedupTtl;

    /// <summary>Exposed so the endpoint body-limit enforcer reads from the same config.</summary>
    public long MaxBodyBytes => _options.CurrentValue.MaxBodyBytes;

    /// <summary>
    /// Minimal IOptionsMonitor shim used only when the verifier is constructed outside DI
    /// (test helpers lacking a registered options monitor). Never used in production paths.
    /// </summary>
    private sealed class StaticOptionsMonitor : IOptionsMonitor<SnsWebhookOptions>
    {
        public StaticOptionsMonitor(SnsWebhookOptions value) => CurrentValue = value;
        public SnsWebhookOptions CurrentValue { get; }
        public SnsWebhookOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SnsWebhookOptions, string?> listener) => null;
    }

    /// <summary>
    /// Verifies the SNS message signature. Returns true when the signature is valid and the signing
    /// certificate URL points to an AWS-owned host.
    /// </summary>
    public async Task<bool> VerifyAsync(SnsMessage message, string requestId, CancellationToken cancellationToken)
    {
        if (!SnsValidation.IsValidSigningCertUrl(message.SigningCertUrl))
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonBadCertUrl);
            LogInvalidCertUrl(_logger, requestId, message.SigningCertUrl ?? "null");
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.Signature) ||
            string.IsNullOrWhiteSpace(message.SignatureVersion) ||
            string.IsNullOrWhiteSpace(message.Type) ||
            string.IsNullOrWhiteSpace(message.MessageId) ||
            string.IsNullOrWhiteSpace(message.Timestamp))
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonMissingField);
            LogMissingFields(_logger, requestId);
            return false;
        }

        if (message.SignatureVersion != "1" && message.SignatureVersion != "2")
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonMissingField);
            LogUnsupportedSignatureVersion(_logger, requestId, message.SignatureVersion);
            return false;
        }

        if (!DateTimeOffset.TryParse(
                message.Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonTimestampSkew);
            LogInvalidTimestamp(_logger, requestId, message.Timestamp);
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        var skew = (now - timestamp).Duration();
        if (skew > _options.CurrentValue.MaxClockSkew)
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonTimestampSkew);
            LogTimestampOutOfWindow(_logger, requestId, message.Timestamp, skew);
            return false;
        }

        var canonical = BuildCanonicalString(message);
        if (canonical is null)
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonMissingField);
            LogCanonicalBuildFailed(_logger, requestId, message.Type);
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(message.Signature);
        }
        catch (FormatException)
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonMissingField);
            LogInvalidSignatureEncoding(_logger, requestId);
            return false;
        }

        X509Certificate2 cert;
        try
        {
            cert = await GetCertificateAsync(message.SigningCertUrl!, cancellationToken);
        }
        catch (Exception ex)
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonCertFetchFailed);
            LogCertFetchFailed(_logger, requestId, ex);
            return false;
        }

        // SignatureVersion selects the canonical-string layout, not the crypto algorithm — the
        // algorithm is driven by the signing cert's public-key type (RSA-SHA256 for classic AWS SNS
        // signing certs; ECDSA only if AWS issues an EC cert for the topic). AWS SigV2 on FIFO
        // topics is still RSA today. Prefer RSA; fall back to ECDSA if the cert happens to be EC.
        var payload = Encoding.UTF8.GetBytes(canonical);
        bool valid;
        using (var rsa = cert.GetRSAPublicKey())
        {
            if (rsa is not null)
            {
                valid = rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            else
            {
                using var ecdsa = cert.GetECDsaPublicKey();
                if (ecdsa is null)
                {
                    SnsMetrics.RejectForReason(SnsMetrics.ReasonBadCert);
                    LogNonRsaCert(_logger, requestId);
                    return false;
                }
                valid = ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256);
            }
        }

        if (!valid)
        {
            SnsMetrics.RejectForReason(SnsMetrics.ReasonSignatureMismatch);
            LogSignatureMismatch(_logger, requestId, message.MessageId);

            // TEMP diagnostic: when Sns__DebugCanonical=true, dump the canonical string byte length,
            // per-field lengths, SHA256, and a short SigningCertURL tail so we can pinpoint the
            // canonical-construction bug without leaking full payloads.
            if (_options.CurrentValue.DebugCanonical)
            {
                var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
                var sigB64First16 = message.Signature is { Length: > 16 } s ? s[..16] : message.Signature ?? "";
                LogCanonicalDebug(
                    _logger,
                    requestId,
                    message.MessageId,
                    message.Type ?? "null",
                    message.SignatureVersion ?? "null",
                    payload.Length,
                    sha,
                    message.Message?.Length ?? -1,
                    message.Subject is null ? "null" : (message.Subject.Length == 0 ? "empty" : $"len={message.Subject.Length}"),
                    message.Timestamp ?? "null",
                    message.TopicArn ?? "null",
                    sigB64First16,
                    message.SigningCertUrl ?? "null",
                    canonical);
            }
        }
        else
        {
            SnsMetrics.RecordVerificationSuccess();
        }

        return valid;
    }

    /// <summary>
    /// Builds the canonical string to be hashed per AWS SNS spec.
    /// Keys appear in alphabetical order, each followed by a newline; values are followed by a newline.
    /// </summary>
    internal static string? BuildCanonicalString(SnsMessage message)
    {
        var sb = new StringBuilder();

        switch (message.Type)
        {
            case "Notification":
                sb.Append("Message\n").Append(message.Message).Append('\n');
                sb.Append("MessageId\n").Append(message.MessageId).Append('\n');
                if (!string.IsNullOrEmpty(message.Subject))
                {
                    sb.Append("Subject\n").Append(message.Subject).Append('\n');
                }
                sb.Append("Timestamp\n").Append(message.Timestamp).Append('\n');
                sb.Append("TopicArn\n").Append(message.TopicArn).Append('\n');
                sb.Append("Type\n").Append(message.Type).Append('\n');
                return sb.ToString();

            case "SubscriptionConfirmation":
            case "UnsubscribeConfirmation":
                if (string.IsNullOrEmpty(message.SubscribeUrl) || string.IsNullOrEmpty(message.Token))
                {
                    return null;
                }
                sb.Append("Message\n").Append(message.Message).Append('\n');
                sb.Append("MessageId\n").Append(message.MessageId).Append('\n');
                sb.Append("SubscribeURL\n").Append(message.SubscribeUrl).Append('\n');
                sb.Append("Timestamp\n").Append(message.Timestamp).Append('\n');
                sb.Append("Token\n").Append(message.Token).Append('\n');
                sb.Append("TopicArn\n").Append(message.TopicArn).Append('\n');
                sb.Append("Type\n").Append(message.Type).Append('\n');
                return sb.ToString();

            default:
                return null;
        }
    }

    private async Task<X509Certificate2> GetCertificateAsync(string url, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        // Positive cache hit fast-path.
        lock (CacheLock)
        {
            if (CacheIndex.TryGetValue(url, out var node) && node.Value.ExpiresAt > now)
            {
                // Move to front (most-recently-used).
                CacheOrder.Remove(node);
                CacheOrder.AddFirst(node);
                return node.Value.Certificate;
            }
        }

        // Negative cache hit — short-circuit without touching the network.
        if (NegativeCache.TryGetValue(url, out var neg) && neg.ExpiresAt > now)
        {
            throw new CertFetchCachedFailureException(neg.Reason);
        }

        SnsMetrics.CertCacheMiss.Add(1);

        // Coalesce concurrent misses so only one outbound HTTP call races to populate the cache.
        var lazy = InFlightFetches.GetOrAdd(url, u => new Lazy<Task<X509Certificate2>>(
            () => FetchAndCacheAsync(u, cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazy.Value;
        }
        finally
        {
            // Leave succeeded Lazy removed so future misses re-enter through the cache fast-path.
            InFlightFetches.TryRemove(new KeyValuePair<string, Lazy<Task<X509Certificate2>>>(url, lazy));
        }
    }

    private async Task<X509Certificate2> FetchAndCacheAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("SnsSigningCert");
            var pem = await httpClient.GetStringAsync(url, cancellationToken);
            var cert = X509Certificate2.CreateFromPem(pem);
            var expiresAt = _timeProvider.GetUtcNow() + _options.CurrentValue.CacheTtl;
            InsertIntoCache(url, cert, expiresAt);
            return cert;
        }
        catch (HttpRequestException ex)
        {
            RecordNegative(url, "http_error");
            SnsMetrics.CertFetchFailures.Add(1, new KeyValuePair<string, object?>("reason", "http_error"));
            LogCertHttpError(_logger, url, ex);
            throw;
        }
        catch (CryptographicException ex)
        {
            RecordNegative(url, "parse_error");
            SnsMetrics.CertFetchFailures.Add(1, new KeyValuePair<string, object?>("reason", "parse_error"));
            LogCertParseError(_logger, url, ex);
            throw;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is caller-driven and not a persistent failure — don't poison the negative cache.
            throw;
        }
        catch (Exception ex)
        {
            RecordNegative(url, "other");
            SnsMetrics.CertFetchFailures.Add(1, new KeyValuePair<string, object?>("reason", "other"));
            LogCertHttpError(_logger, url, ex);
            throw;
        }
    }

    private void RecordNegative(string url, string reason)
    {
        var expiresAt = _timeProvider.GetUtcNow() + _options.CurrentValue.NegativeCacheTtl;
        NegativeCache[url] = new NegativeCacheEntry(reason, expiresAt);
    }

    private static void InsertIntoCache(string url, X509Certificate2 certificate, DateTimeOffset expiresAt)
    {
        lock (CacheLock)
        {
            if (CacheIndex.TryGetValue(url, out var existing))
            {
                // Replace entry but do NOT Dispose the displaced X509Certificate2. A concurrent
                // VerifyAsync may still hold a reference after GetCertificateAsync released the
                // cache lock; an eager Dispose here creates a use-after-dispose on its RSA/ECDSA
                // public-key handle. Let the finalizer reclaim native handles — the cache is
                // bounded (CacheCapacity, default 32), so cumulative leak is negligible.
                CacheOrder.Remove(existing);
                CacheIndex.Remove(url);
            }

            while (CacheIndex.Count >= CacheCapacity)
            {
                // Evict LRU (tail). Do NOT Dispose — same use-after-dispose concern as above.
                var lru = CacheOrder.Last!;
                CacheOrder.RemoveLast();
                CacheIndex.Remove(lru.Value.Url);
            }

            var node = new LinkedListNode<CachedCert>(new CachedCert(url, certificate, expiresAt));
            CacheOrder.AddFirst(node);
            CacheIndex[url] = node;
        }
    }

    /// <summary>Test hook to seed / clear the cert cache.</summary>
    internal static void SetCachedCertificate(string url, X509Certificate2 certificate, DateTimeOffset expiresAt)
        => InsertIntoCache(url, certificate, expiresAt);

    internal static void ClearCertCache()
    {
        lock (CacheLock)
        {
            CacheIndex.Clear();
            CacheOrder.Clear();
        }
        NegativeCache.Clear();
        InFlightFetches.Clear();
    }

    internal static int CertCacheCount
    {
        get { lock (CacheLock) { return CacheIndex.Count; } }
    }

    internal static bool CertCacheContains(string url)
    {
        lock (CacheLock) { return CacheIndex.ContainsKey(url); }
    }

    internal static bool NegativeCacheContains(string url) => NegativeCache.ContainsKey(url);

    private sealed record CachedCert(string Url, X509Certificate2 Certificate, DateTimeOffset ExpiresAt);

    private sealed record NegativeCacheEntry(string Reason, DateTimeOffset ExpiresAt);

    /// <summary>
    /// Thrown from <see cref="GetCertificateAsync"/> when the negative cache has a live entry for the
    /// URL; bubbles up to <see cref="VerifyAsync"/>'s catch-all which rejects the signature.
    /// </summary>
    internal sealed class CertFetchCachedFailureException : Exception
    {
        public CertFetchCachedFailureException(string reason)
            : base($"Cert fetch previously failed ({reason}); cached in negative cache.") { }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (invalid cert URL). RequestId={RequestId} Url={Url}")]
    private static partial void LogInvalidCertUrl(ILogger logger, string requestId, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (missing required fields). RequestId={RequestId}")]
    private static partial void LogMissingFields(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (unsupported SignatureVersion {Version}). RequestId={RequestId}")]
    private static partial void LogUnsupportedSignatureVersion(ILogger logger, string requestId, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (invalid timestamp {Timestamp}). RequestId={RequestId}")]
    private static partial void LogInvalidTimestamp(ILogger logger, string requestId, string timestamp);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (timestamp {Timestamp} outside tolerance window, skew={Skew}). RequestId={RequestId}")]
    private static partial void LogTimestampOutOfWindow(ILogger logger, string requestId, string timestamp, TimeSpan skew);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (canonical build failed for Type={Type}). RequestId={RequestId}")]
    private static partial void LogCanonicalBuildFailed(ILogger logger, string requestId, string type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (invalid base64 signature). RequestId={RequestId}")]
    private static partial void LogInvalidSignatureEncoding(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "SNS signature verification failed (cert fetch error). RequestId={RequestId}")]
    private static partial void LogCertFetchFailed(ILogger logger, string requestId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (certificate has no usable public key). RequestId={RequestId}")]
    private static partial void LogNonRsaCert(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS signature rejected (signature mismatch). RequestId={RequestId} MessageId={MessageId}")]
    private static partial void LogSignatureMismatch(ILogger logger, string requestId, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "SNS cert fetch error (HTTP). Url={Url}")]
    private static partial void LogCertHttpError(ILogger logger, string url, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "SNS cert parse error (invalid PEM). Url={Url}")]
    private static partial void LogCertParseError(ILogger logger, string url, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS_CANONICAL_DEBUG RequestId={RequestId} MessageId={MessageId} Type={Type} SigVer={SigVer} CanonLen={CanonLen} CanonSha256={CanonSha256} MessageLen={MessageLen} Subject={SubjectState} Timestamp={Timestamp} TopicArn={TopicArn} SigPrefix={SigPrefix} CertUrl={CertUrl} Canonical={Canonical}")]
    private static partial void LogCanonicalDebug(ILogger logger, string requestId, string messageId, string type, string sigVer, int canonLen, string canonSha256, int messageLen, string subjectState, string timestamp, string topicArn, string sigPrefix, string certUrl, string canonical);
}
