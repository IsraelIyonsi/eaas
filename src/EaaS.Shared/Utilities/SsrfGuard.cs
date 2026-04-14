using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EaaS.Shared.Utilities;

/// <summary>
/// Validates customer-supplied URLs against SSRF attacks (Finding C3).
/// Rejects HTTPS URLs pointing to RFC1918, loopback, link-local (AWS/GCP metadata
/// 169.254.169.254), CGNAT, multicast, or reserved IP ranges, plus suspicious
/// hostnames (localhost, *.internal, *.local). Exposes a <see cref="HttpMessageHandler"/>
/// factory that pins outbound connections to the validated IP, preventing DNS
/// rebinding between validation and connect.
///
/// Static members are retained for backwards compatibility and delegate to a
/// process-wide default <see cref="SsrfGuardService"/> instance configured with
/// <see cref="SsrfGuardOptions"/> defaults. DI-registered callers should inject
/// <see cref="SsrfGuardService"/> directly to pick up ops-tunable config.
/// </summary>
public static class SsrfGuard
{
    public sealed record ValidationResult(bool IsAllowed, string? Reason, IPAddress[]? ResolvedAddresses);

    internal static readonly string[] BlockedHostSuffixes = { ".internal", ".local", ".localhost", ".lan", ".intranet" };
    internal static readonly string[] BlockedExactHosts = { "localhost", "metadata.google.internal", "metadata" };

    /// <summary>
    /// Meter for SSRF guard observability. Emits <c>webhook_ssrf_rejected_total{reason}</c>
    /// from every catch-site that refuses a customer-supplied URL (C3 rev-3) and
    /// <c>webhook_ssrf_guard_disabled_total</c> when the kill-switch is off (C3 rev-4).
    /// </summary>
    public static readonly Meter WebhookDispatchMeter = new("EaaS.WebhookDispatch", "1.0.0");

    private static readonly Counter<long> SsrfRejectedCounter =
        WebhookDispatchMeter.CreateCounter<long>(
            "webhook_ssrf_rejected_total",
            unit: "rejections",
            description: "Customer webhook URLs refused by the SSRF guard, labelled by reason.");

    internal static readonly Counter<long> SsrfGuardDisabledCounter =
        WebhookDispatchMeter.CreateCounter<long>(
            "webhook_ssrf_guard_disabled_total",
            unit: "calls",
            description: "SSRF guard calls bypassed because the kill-switch is off (C3 rev-4).");

    /// <summary>
    /// Increments <c>webhook_ssrf_rejected_total{reason}</c>. Safe to call from any catch site.
    /// </summary>
    public static void RecordSsrfRejection(string reason)
    {
        SsrfRejectedCounter.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    /// <summary>
    /// Per-candidate connect timeout for happy-eyeballs in <see cref="CreateGuardedHandler"/>.
    /// Tightened from 5s to 2s (C3 rev-4) so a dead v6 address fails over to v4 inside
    /// the RFC 8305 happy-eyeballs budget; <see cref="TotalConnectBudget"/> still caps the
    /// whole connect phase at 5s across all candidate addresses.
    /// </summary>
    internal static readonly TimeSpan PerAttemptConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Hard ceiling on total connect time across all resolved addresses (C3 rev-4).
    /// Caller-supplied cancellation still takes precedence; this is an upper bound.
    /// </summary>
    internal static readonly TimeSpan TotalConnectBudget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Process-wide default instance used by the static passthroughs. Kept out of DI
    /// so code paths that never touched DI (validators, SNS signing fetch during
    /// startup) keep working without a container.
    /// </summary>
    private static readonly SsrfGuardService DefaultInstance =
        new(Options.Create(new SsrfGuardOptions()), NullLogger<SsrfGuardService>.Instance);

    public static bool IsSyntacticallySafe(string? url, out string? reason)
        => DefaultInstance.IsSyntacticallySafe(url, out reason);

    public static Task<ValidationResult> ValidateAsync(string url, CancellationToken cancellationToken = default)
        => DefaultInstance.ValidateAsync(url, cancellationToken);

    public static bool IsPublicAddress(IPAddress ip)
        => SsrfGuardService.IsPublicAddressCore(ip);

    public static SocketsHttpHandler CreateGuardedHandler()
        => DefaultInstance.CreateGuardedHandler();

    public const int MaxRedirectHops = SsrfGuardService.MaxRedirectHops;
    public const int MaxResponseBodyBytes = SsrfGuardService.MaxResponseBodyBytes;

    public static Task<string> ReadBoundedStringAsync(
        HttpResponseMessage response,
        int maxBytes = MaxResponseBodyBytes,
        bool truncate = true,
        CancellationToken cancellationToken = default)
        => SsrfGuardService.ReadBoundedStringAsync(response, maxBytes, truncate, cancellationToken);

    public static Task<HttpResponseMessage> SendWithSafeRedirectsAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        => DefaultInstance.SendWithSafeRedirectsAsync(client, request, cancellationToken);
}

/// <summary>
/// Instance form of <see cref="SsrfGuard"/> that honours <see cref="SsrfGuardOptions"/>.
/// Register as a singleton; inject wherever SSRF validation runs. The surface mirrors
/// the static helper for drop-in replacement.
/// </summary>
public sealed class SsrfGuardService
{
    public const int MaxRedirectHops = 3;
    public const int MaxResponseBodyBytes = 64 * 1024;

    private static readonly string[] SensitiveHeadersToStripOnCrossOrigin =
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
    };

    private static readonly string[] SensitiveHeaderPrefixesToStripOnCrossOrigin =
    {
        "X-EaaS-Signature",
        "X-Webhook-Signature",
    };

    private static readonly Action<ILogger, string, Exception?> LogSsrfGuardDisabled =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5001, "SsrfGuardDisabled"),
            "SsrfGuardDisabled: kill-switch engaged (context={Context}). SSRF validation bypassed; timeouts still apply.");

    private readonly IOptions<SsrfGuardOptions> _options;
    private readonly ILogger<SsrfGuardService> _logger;

    public SsrfGuardService(IOptions<SsrfGuardOptions> options, ILogger<SsrfGuardService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<SsrfGuardService>.Instance;
    }

    private SsrfGuardOptions Opts => _options.Value ?? new SsrfGuardOptions();

    /// <summary>
    /// If the kill-switch is off, emit a loud log + counter and return true.
    /// Caller then skips validation. We intentionally log at Error — running in
    /// bypass mode should never be quiet.
    /// </summary>
    private bool IsBypassed(string context)
    {
        if (Opts.Enabled)
            return false;

        SsrfGuard.SsrfGuardDisabledCounter.Add(1,
            new KeyValuePair<string, object?>("context", context));
        LogSsrfGuardDisabled(_logger, context, null);
        return true;
    }

    public bool IsSyntacticallySafe(string? url, out string? reason)
    {
        if (IsBypassed(nameof(IsSyntacticallySafe)))
        {
            reason = null;
            return true;
        }

        reason = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            reason = "URL is required.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            reason = "URL must be a valid absolute URL.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            reason = "URL must use HTTPS.";
            return false;
        }

        // IDN-normalize the host to punycode so Cyrillic/lookalike hostnames
        // (e.g. "lоcalhost" with Cyrillic 'о') are compared in their ASCII form.
        string host;
        try
        {
            host = uri.IdnHost;
        }
        catch (UriFormatException)
        {
            host = uri.Host;
        }

        if (!IsHostOverrideAllowed(host) && IsBlockedHostname(host))
        {
            reason = "URL hostname is not permitted.";
            return false;
        }

        // If the host is a literal IP, check immediately.
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            if (!IsPublicAddressWithOverrides(ip))
            {
                reason = "URL must not point to a private, loopback, metadata, or reserved IP address.";
                return false;
            }
        }

        return true;
    }

    public async Task<SsrfGuard.ValidationResult> ValidateAsync(string url, CancellationToken cancellationToken = default)
    {
        if (IsBypassed(nameof(ValidateAsync)))
            return new SsrfGuard.ValidationResult(true, null, null);

        if (!IsSyntacticallySafe(url, out var reason))
            return new SsrfGuard.ValidationResult(false, reason, null);

        var uri = new Uri(url);
        var host = uri.DnsSafeHost;

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = new[] { literal };
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                return new SsrfGuard.ValidationResult(false, $"DNS resolution failed: {ex.Message}", null);
            }
        }

        if (addresses.Length == 0)
            return new SsrfGuard.ValidationResult(false, "Hostname did not resolve to any IP.", null);

        foreach (var addr in addresses)
        {
            if (!IsPublicAddressWithOverrides(addr))
                return new SsrfGuard.ValidationResult(false,
                    $"URL resolves to non-public IP {addr} (private/loopback/metadata/reserved range).",
                    null);
        }

        return new SsrfGuard.ValidationResult(true, null, addresses);
    }

    /// <summary>
    /// Returns true if the IP is a globally-routable unicast public address.
    /// Rejects RFC1918, loopback, link-local, CGNAT, multicast, broadcast,
    /// 0.0.0.0/8, IPv4-mapped private v6, IPv6 ULA, link-local v6, multicast,
    /// unspecified.
    /// </summary>
    public static bool IsPublicAddressCore(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();

            if (b[0] == 0) return false;
            if (b[0] == 10) return false;
            if (b[0] == 127) return false;
            if (b[0] == 169 && b[1] == 254) return false;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;
            if (b[0] == 192 && b[1] == 0) return false;
            if (b[0] == 192 && b[1] == 168) return false;
            if (b[0] == 198 && (b[1] == 18 || b[1] == 19)) return false;
            if (b[0] == 198 && b[1] == 51 && b[2] == 100) return false;
            if (b[0] == 203 && b[1] == 0 && b[2] == 113) return false;
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;
            if (b[0] >= 224) return false;

            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip)) return false;
            if (ip.IsIPv6LinkLocal) return false;
            if (ip.IsIPv6SiteLocal) return false;
            if (ip.IsIPv6Multicast) return false;
            if (ip.Equals(IPAddress.IPv6Any)) return false;

            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return false;
            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) return false;

            if (b[0] == 0x20 && b[1] == 0x02)
            {
                var embedded = new IPAddress(new[] { b[2], b[3], b[4], b[5] });
                return IsPublicAddressCore(embedded);
            }

            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b
                && b[4] == 0 && b[5] == 0 && b[6] == 0 && b[7] == 0
                && b[8] == 0 && b[9] == 0 && b[10] == 0 && b[11] == 0)
            {
                var embedded = new IPAddress(new[] { b[12], b[13], b[14], b[15] });
                return IsPublicAddressCore(embedded);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Like <see cref="IsPublicAddressCore"/> but also honours
    /// <see cref="SsrfGuardOptions.ExtraAllowedCidrs"/>.
    /// </summary>
    private bool IsPublicAddressWithOverrides(IPAddress ip)
    {
        if (IsPublicAddressCore(ip))
            return true;

        foreach (var cidr in Opts.ExtraAllowedCidrs)
        {
            if (CidrMatches(cidr, ip))
                return true;
        }
        return false;
    }

    private bool IsHostOverrideAllowed(string host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var allowed in Opts.AllowedHostOverrides)
        {
            if (string.Equals(allowed?.Trim().TrimEnd('.'), normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Minimal CIDR matcher for the allowlist. Silently skips malformed entries
    /// so a typo in config never wedges the process.
    /// </summary>
    private static bool CidrMatches(string cidr, IPAddress ip)
    {
        if (string.IsNullOrWhiteSpace(cidr)) return false;
        var slash = cidr.IndexOf('/');
        if (slash < 0) return false;

        if (!IPAddress.TryParse(cidr[..slash], out var network)) return false;
        if (!int.TryParse(cidr[(slash + 1)..], out var prefix)) return false;

        if (network.AddressFamily != ip.AddressFamily) return false;

        var networkBytes = network.GetAddressBytes();
        var ipBytes = ip.GetAddressBytes();
        if (networkBytes.Length != ipBytes.Length) return false;
        if (prefix < 0 || prefix > networkBytes.Length * 8) return false;

        var fullBytes = prefix / 8;
        var remainderBits = prefix % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (networkBytes[i] != ipBytes[i]) return false;
        }

        if (remainderBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainderBits));
        return (networkBytes[fullBytes] & mask) == (ipBytes[fullBytes] & mask);
    }

    private static bool IsBlockedHostname(string host)
    {
        if (string.IsNullOrEmpty(host)) return true;
        var h = host.Trim().TrimEnd('.').ToLowerInvariant();

        foreach (var exact in SsrfGuard.BlockedExactHosts)
            if (h == exact) return true;

        foreach (var suffix in SsrfGuard.BlockedHostSuffixes)
            if (h.EndsWith(suffix, StringComparison.Ordinal)) return true;

        return false;
    }

    public SocketsHttpHandler CreateGuardedHandler()
    {
        // Capture the service-scoped settings so the handler honours DI-configured
        // overrides even when the SocketsHttpHandler is long-lived.
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxResponseHeadersLength = 16,
            ConnectCallback = async (ctx, ct) =>
            {
                var host = ctx.DnsEndPoint.Host;
                var port = ctx.DnsEndPoint.Port;

                IPAddress[] addresses;
                if (IPAddress.TryParse(host, out var literal))
                {
                    addresses = new[] { literal };
                }
                else
                {
                    addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
                }

                if (addresses.Length == 0)
                    throw new HttpRequestException($"Hostname '{host}' did not resolve.");

                var bypass = !Opts.Enabled;
                if (!bypass)
                {
                    foreach (var a in addresses)
                    {
                        if (!IsPublicAddressWithOverrides(a))
                        {
                            SsrfGuard.RecordSsrfRejection("private_ip");
                            throw new HttpRequestException(
                                $"Refused to connect to '{host}': resolved IP {a} is in a blocked range.");
                        }
                    }
                }

                // Total-budget CTS caps the full connect phase across all candidates
                // at TotalConnectBudget (5s). Per-attempt CTS enforces PerAttemptConnectTimeout
                // (2s). Both link to the caller's CT so cancellations still propagate.
                using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                totalCts.CancelAfter(SsrfGuard.TotalConnectBudget);

                Exception? lastError = null;
                for (var i = 0; i < addresses.Length; i++)
                {
                    if (totalCts.IsCancellationRequested)
                    {
                        lastError = new SocketException((int)SocketError.TimedOut);
                        break;
                    }

                    var addr = addresses[i];
                    var socket = new Socket(
                        addr.AddressFamily == AddressFamily.InterNetworkV6
                            ? AddressFamily.InterNetworkV6
                            : AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp) { NoDelay = true };

                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(totalCts.Token);
                    attemptCts.CancelAfter(SsrfGuard.PerAttemptConnectTimeout);

                    try
                    {
                        await socket.ConnectAsync(addr, port, attemptCts.Token).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        socket.Dispose();
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        // Per-attempt or total-budget fired. Record and try the next candidate
                        // unless the total budget has also expired — in which case abort.
                        socket.Dispose();
                        lastError = new SocketException((int)SocketError.TimedOut);
                        if (totalCts.IsCancellationRequested)
                            break;
                    }
                    catch (SocketException ex)
                    {
                        socket.Dispose();
                        lastError = ex;
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }

                throw new HttpRequestException(
                    $"Unable to connect to '{host}' on any of {addresses.Length} resolved address(es).",
                    lastError);
            }
        };
    }

    public static async Task<string> ReadBoundedStringAsync(
        HttpResponseMessage response,
        int maxBytes = MaxResponseBodyBytes,
        bool truncate = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new byte[maxBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }

        if (total > maxBytes)
        {
            if (!truncate)
                throw new HttpRequestException("response exceeded 64 KB limit");
            total = maxBytes;
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, total);
    }

    public async Task<HttpResponseMessage> SendWithSafeRedirectsAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        var originalHost = request.RequestUri?.Host ?? string.Empty;
        var current = request;
        HttpResponseMessage? response = null;
        for (var hop = 0; hop <= MaxRedirectHops; hop++)
        {
            response = await client.SendAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var status = (int)response.StatusCode;
            if (status is < 300 or >= 400)
                return response;

            var location = response.Headers.Location;
            if (location is null)
                return response;

            if (hop == MaxRedirectHops)
            {
                response.Dispose();
                throw new HttpRequestException(
                    $"Webhook exceeded the {MaxRedirectHops}-redirect limit. Use your final URL.");
            }

            var next = location.IsAbsoluteUri ? location : new Uri(current.RequestUri!, location);
            var validation = await ValidateAsync(next.ToString(), cancellationToken).ConfigureAwait(false);
            if (!validation.IsAllowed)
            {
                response.Dispose();
                throw new HttpRequestException(
                    $"Webhook redirect to '{next}' refused: {validation.Reason}");
            }

            // RFC 7231 §6.4.2-4: 301/302/303 downgrade to GET; 307/308 preserve method + body.
            var nextMethod = (status == 307 || status == 308) ? current.Method : HttpMethod.Get;
            var nextRequest = new HttpRequestMessage(nextMethod, next);

            // Cross-origin redirect hardening (C3 rev-4, M1): strip credential-bearing
            // headers when the redirect target host differs from the ORIGINAL request host
            // (case-insensitive). Prevents a compromised or malicious redirect hop from
            // harvesting the tenant's Authorization / Cookie / webhook signature.
            var isCrossOrigin = !string.Equals(next.Host, originalHost, StringComparison.OrdinalIgnoreCase);

            foreach (var header in current.Headers)
            {
                if (isCrossOrigin && ShouldStripOnCrossOrigin(header.Key))
                    continue;
                nextRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if ((status == 307 || status == 308) && current.Content is not null)
                nextRequest.Content = current.Content;

            response.Dispose();
            current = nextRequest;
        }

        return response!;
    }

    private static bool ShouldStripOnCrossOrigin(string headerName)
    {
        foreach (var exact in SensitiveHeadersToStripOnCrossOrigin)
        {
            if (string.Equals(headerName, exact, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        foreach (var prefix in SensitiveHeaderPrefixesToStripOnCrossOrigin)
        {
            if (headerName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
