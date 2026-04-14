using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EaaS.Api.Constants;
using EaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EaaS.Api.Authentication;

public sealed partial class AdminSessionAuthHandler : AuthenticationHandler<AdminSessionAuthSchemeOptions>
{
    public const string SchemeName = "AdminSession";
    private const string CookieName = AuthCookieConstants.AdminSessionCookie;

    // Maximum age (seconds) for a signed proxy token. Short window limits replay risk.
    private const long ProxyTokenMaxAgeSeconds = 60;

    // Domain-separation prefixes. Without these, an HMAC produced for one context
    // (e.g. a session cookie) could be replayed into another (e.g. a proxy token).
    private const string CookieHmacDomain = "eaas.cookie.v1\n";
    private const string ProxyTokenHmacDomain = "eaas.proxy.v1\n";

    // Observability: count every time the proxy-header fallback is invoked without
    // a valid signed token. Tag outcome so dashboards can distinguish hard
    // rejections from grace-window allowances during rollout.
    public const string MeterName = "EaaS.Api.Auth";
    public const string ProxyTokenMissingCounterName = "admin_auth.proxy_token_missing_total";
    private static readonly Meter AuthMeter = new(MeterName);
    private static readonly Counter<long> ProxyTokenMissingCounter =
        AuthMeter.CreateCounter<long>(ProxyTokenMissingCounterName);

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AdminSessionAuthHandler(
        IOptionsMonitor<AdminSessionAuthSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext dbContext,
        TimeProvider? timeProvider = null)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Read session cookie instead of trusting a spoofable header
        var sessionSecret = Options.SessionSecret;

        var cookie = Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(cookie))
        {
            // Fall back to signed proxy-token flow (dashboard BFF -> API).
            // The proxy MUST sign userId+timestamp with the shared SessionSecret using HMAC-SHA256.
            // The unsigned X-Admin-User-Id header alone is NEVER trusted.
            return await HandleProxyHeaderFallback(sessionSecret);
        }
        if (string.IsNullOrWhiteSpace(sessionSecret))
        {
            LogMissingSessionSecret(Logger);
            return AuthenticateResult.Fail("Admin session secret is not configured.");
        }

        // Cookie format: base64url(payload).hex(hmac-sha256)
        var parts = cookie.Split('.');
        if (parts.Length != 2)
        {
            LogInvalidCookieFormat(Logger);
            return AuthenticateResult.Fail("Invalid session cookie format.");
        }

        var encoded = parts[0];
        var signature = parts[1];

        // Verify HMAC signature (domain-separated against proxy tokens)
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sessionSecret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(CookieHmacDomain + encoded));
        var expected = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expected)))
        {
            LogInvalidSignature(Logger);
            return AuthenticateResult.Fail("Invalid session signature.");
        }

        // Decode payload
        SessionPayload? payload;
        try
        {
            // base64url -> standard base64
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            var padding = (4 - base64.Length % 4) % 4;
            base64 += new string('=', padding);

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            payload = JsonSerializer.Deserialize<SessionPayload>(json);
        }
        catch
        {
            LogInvalidCookieFormat(Logger);
            return AuthenticateResult.Fail("Invalid session payload.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.UserId))
            return AuthenticateResult.Fail("Invalid session payload.");

        // Check expiry
        if (payload.ExpiresAt.HasValue && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > payload.ExpiresAt.Value)
        {
            LogSessionExpired(Logger, payload.UserId);
            return AuthenticateResult.Fail("Session expired.");
        }

        if (!Guid.TryParse(payload.UserId, out var userId))
        {
            LogInvalidUserId(Logger, payload.UserId);
            return AuthenticateResult.Fail("Invalid user ID.");
        }

        return await AuthenticateAdminUser(userId);
    }

    /// <summary>
    /// Validates a signed proxy token from the trusted dashboard BFF.
    /// The previous implementation trusted an unsigned X-Admin-User-Id header; any caller able
    /// to reach the API could impersonate an admin. This flow now requires an HMAC-SHA256 token
    /// over "userId.timestamp" using the shared SessionSecret and enforces a short expiry window.
    /// </summary>
    private async Task<AuthenticateResult> HandleProxyHeaderFallback(string sessionSecret)
    {
        var hasUserIdHeader = Request.Headers.TryGetValue(
            HttpHeaderConstants.AdminUserId, out var userIdHeader);
        var hasTokenHeader = Request.Headers.TryGetValue(
            HttpHeaderConstants.AdminProxyToken, out var tokenHeader);

        if (!hasUserIdHeader)
            return AuthenticateResult.NoResult();

        var userIdStr = userIdHeader.ToString();
        var tokenStr = hasTokenHeader ? tokenHeader.ToString() : string.Empty;

        if (string.IsNullOrWhiteSpace(userIdStr))
            return AuthenticateResult.NoResult();

        // Feature-flag escape hatch for zero-downtime rollout ONLY. When
        // RequireProxyToken is false and no signed token is present, accept the
        // unsigned header but emit a loud warning so operators SEE every use.
        if (string.IsNullOrWhiteSpace(tokenStr))
        {
            // EnforceAfter is a hard cut-over: once we are past that instant,
            // the legacy unsigned path is ALWAYS rejected regardless of the
            // RequireProxyToken flag. This prevents a stale env var from
            // leaving the bypass open indefinitely.
            var currentTime = _timeProvider.GetUtcNow();
            var pastEnforceCutover =
                Options.EnforceAfter.HasValue && currentTime >= Options.EnforceAfter.Value;
            var inGraceWindow =
                Options.EnforceAfter.HasValue && currentTime < Options.EnforceAfter.Value;

            var legacyAllowed = !Options.RequireProxyToken && !pastEnforceCutover;
            // EnforceAfter grace window also allows the legacy path even when
            // RequireProxyToken=true, so operators can stage the flip.
            if (!legacyAllowed && inGraceWindow)
            {
                legacyAllowed = true;
            }

            if (legacyAllowed)
            {
                ProxyTokenMissingCounter.Add(
                    1, new KeyValuePair<string, object?>("outcome", "allowed_during_grace"));
                LogUnsignedAdminHeaderAccepted(Logger, userIdStr, Request.Path);
                if (!Guid.TryParse(userIdStr, out var legacyUserId))
                {
                    LogInvalidUserId(Logger, userIdStr);
                    return AuthenticateResult.Fail("Invalid user ID.");
                }
                return await AuthenticateAdminUser(legacyUserId);
            }

            ProxyTokenMissingCounter.Add(
                1, new KeyValuePair<string, object?>("outcome", "rejected"));
            return AuthenticateResult.NoResult();
        }

        if (string.IsNullOrWhiteSpace(sessionSecret))
        {
            LogMissingSessionSecret(Logger);
            return AuthenticateResult.Fail("Admin session secret is not configured.");
        }

        // Token format: base64url(timestampUnix).hex(hmac-sha256(userId + "." + timestampUnix))
        var tokenParts = tokenStr.Split('.');
        if (tokenParts.Length != 2)
        {
            LogInvalidProxyToken(Logger);
            return AuthenticateResult.Fail("Invalid proxy token format.");
        }

        long timestamp;
        try
        {
            var base64 = tokenParts[0].Replace('-', '+').Replace('_', '/');
            var padding = (4 - base64.Length % 4) % 4;
            base64 += new string('=', padding);
            var tsStr = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            if (!long.TryParse(tsStr, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out timestamp))
            {
                LogInvalidProxyToken(Logger);
                return AuthenticateResult.Fail("Invalid proxy token payload.");
            }
        }
        catch
        {
            LogInvalidProxyToken(Logger);
            return AuthenticateResult.Fail("Invalid proxy token payload.");
        }

        // Guard against non-positive timestamps (e.g. 0, negative, underflow) before
        // any age arithmetic — a 0 timestamp with a clock-skew window check would
        // otherwise be indistinguishable from a legitimately old token.
        if (timestamp <= 0)
        {
            LogInvalidProxyToken(Logger);
            return AuthenticateResult.Fail("Invalid proxy token timestamp.");
        }

        // Expiry window (reject both too-old and far-future tokens for clock-skew safety)
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var age = now - timestamp;
        if (age > ProxyTokenMaxAgeSeconds || age < -ProxyTokenMaxAgeSeconds)
        {
            LogProxyTokenExpired(Logger, userIdStr);
            return AuthenticateResult.Fail("Proxy token expired.");
        }

        // Recompute expected signature and constant-time compare.
        // Signing input binds domain + HTTP method + path + userId + timestamp so a
        // captured token can only replay the exact same operation within 60s.
        var method = Request.Method.ToUpperInvariant();
        var path = Request.Path.HasValue ? Request.Path.Value! : "/";
        var signingInput = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{ProxyTokenHmacDomain}{method}\n{path}\n{userIdStr}.{timestamp}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sessionSecret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        var expectedSig = Convert.ToHexString(expectedBytes).ToLowerInvariant();
        var providedSig = tokenParts[1];

        if (providedSig.Length != expectedSig.Length
            || !CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(providedSig),
                   Encoding.UTF8.GetBytes(expectedSig)))
        {
            LogInvalidProxyTokenSignature(Logger);
            return AuthenticateResult.Fail("Invalid proxy token signature.");
        }

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            LogInvalidUserId(Logger, userIdStr);
            return AuthenticateResult.Fail("Invalid user ID.");
        }

        return await AuthenticateAdminUser(userId);
    }

    private async Task<AuthenticateResult> AuthenticateAdminUser(Guid userId)
    {
        var adminUser = await _dbContext.AdminUsers
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Email, u.Role, u.IsActive })
            .FirstOrDefaultAsync();

        if (adminUser is null)
        {
            LogAdminUserNotFound(Logger, userId);
            return AuthenticateResult.NoResult();
        }

        if (!adminUser.IsActive)
        {
            LogAdminUserInactive(Logger, userId);
            return AuthenticateResult.NoResult();
        }

        var claims = new[]
        {
            new Claim(ClaimNameConstants.AdminUserId, userId.ToString()),
            new Claim(ClaimNameConstants.AdminEmail, adminUser.Email),
            new Claim(ClaimNameConstants.AdminRole, adminUser.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        LogAdminAuthenticated(Logger, userId, adminUser.Email, Request.Path);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    private sealed class SessionPayload
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public long? ExpiresAt { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Admin session secret is not configured. Set AdminSession:SessionSecret in configuration.")]
    private static partial void LogMissingSessionSecret(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid admin session cookie format")]
    private static partial void LogInvalidCookieFormat(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid admin session cookie signature")]
    private static partial void LogInvalidSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin session expired for user {UserId}")]
    private static partial void LogSessionExpired(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid admin user ID format: {UserId}")]
    private static partial void LogInvalidUserId(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin user not found: {UserId}")]
    private static partial void LogAdminUserNotFound(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin user is inactive: {UserId}")]
    private static partial void LogAdminUserInactive(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Admin session authenticated: UserId={UserId}, Email={Email} on {RequestPath}")]
    private static partial void LogAdminAuthenticated(ILogger logger, Guid userId, string email, string requestPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid admin proxy token format")]
    private static partial void LogInvalidProxyToken(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid admin proxy token signature")]
    private static partial void LogInvalidProxyTokenSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin proxy token expired for user {UserId}")]
    private static partial void LogProxyTokenExpired(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AdminSession:RequireProxyToken=false — accepted UNSIGNED X-Admin-User-Id={UserId} on {RequestPath}. FLIP FLAG TO TRUE IMMEDIATELY.")]
    private static partial void LogUnsignedAdminHeaderAccepted(ILogger logger, string userId, string requestPath);
}
