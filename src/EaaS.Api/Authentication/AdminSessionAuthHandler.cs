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

    private readonly AppDbContext _dbContext;

    public AdminSessionAuthHandler(
        IOptionsMonitor<AdminSessionAuthSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext dbContext)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Read session cookie instead of trusting a spoofable header
        var cookie = Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(cookie))
        {
            // Fall back to X-Admin-User-Id only if request comes from the trusted proxy
            // (proxy sets this header after verifying the dashboard session cookie).
            // The proxy is identified by a valid API key in Authorization header,
            // so this path is only reachable after ApiKeyAuth succeeds on the proxy route.
            return await HandleProxyHeaderFallback();
        }

        var sessionSecret = Options.SessionSecret;
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

        // Verify HMAC signature
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sessionSecret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(encoded));
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

    private async Task<AuthenticateResult> HandleProxyHeaderFallback()
    {
        if (!Request.Headers.TryGetValue(HttpHeaderConstants.AdminUserId, out var userIdHeader))
            return AuthenticateResult.NoResult();

        var userIdStr = userIdHeader.ToString();

        if (string.IsNullOrWhiteSpace(userIdStr))
            return AuthenticateResult.NoResult();

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            LogInvalidUserId(Logger, userIdStr);
            return AuthenticateResult.NoResult();
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
}
