using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EaaS.Api.Authentication;
using EaaS.Api.Extensions;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Authentication;

public sealed class AdminSessionAuthHandlerTests
{
    private const string TestSecret = "super-secret-session-key-for-testing-only";

    private readonly AppDbContext _dbContext;
    private readonly AdminSessionAuthSchemeOptions _options;
    private readonly IOptionsMonitor<AdminSessionAuthSchemeOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;

    public AdminSessionAuthHandlerTests()
    {
        _dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        _options = new AdminSessionAuthSchemeOptions { SessionSecret = TestSecret };
        _optionsMonitor = Substitute.For<IOptionsMonitor<AdminSessionAuthSchemeOptions>>();
        _optionsMonitor.Get(Arg.Any<string>()).Returns(_options);
        _optionsMonitor.CurrentValue.Returns(_options);

        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
    }

    private async Task<AuthenticateResult> AuthenticateAsync(HttpContext httpContext, TimeProvider? timeProvider = null)
    {
        var handler = new AdminSessionAuthHandler(
            _optionsMonitor,
            _loggerFactory,
            UrlEncoder.Default,
            _dbContext,
            timeProvider);

        var scheme = new AuthenticationScheme(AdminSessionAuthHandler.SchemeName, null, typeof(AdminSessionAuthHandler));
        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    private void SetupDbWithAdminUsers(List<AdminUser> users)
    {
        var mockSet = MockDbSetFactory.Create(users);
        _dbContext.AdminUsers.Returns(mockSet);
    }

    private const string CookieHmacDomain = "eaas.cookie.v1\n";
    private const string ProxyTokenHmacDomain = "eaas.proxy.v1\n";

    private static string CreateSessionCookie(string userId, string email, string role, long? expiresAt, string secret)
    {
        var payload = JsonSerializer.Serialize(new { UserId = userId, Email = email, Role = role, ExpiresAt = expiresAt });
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(CookieHmacDomain + encoded))).ToLowerInvariant();
        return $"{encoded}.{sig}";
    }

    private static DefaultHttpContext CreateHttpContextWithCookie(string cookieName, string cookieValue)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = $"{cookieName}={cookieValue}";
        return httpContext;
    }

    [Fact]
    public async Task Should_ReturnNoResult_WhenNoCookieAndNoHeader()
    {
        var httpContext = new DefaultHttpContext();

        var result = await AuthenticateAsync(httpContext);

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFail_WhenSessionSecretNotConfigured()
    {
        _options.SessionSecret = string.Empty;

        var userId = Guid.NewGuid();
        var cookie = CreateSessionCookie(userId.ToString(), "admin@test.com", "SuperAdmin",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), TestSecret);
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", cookie);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("session secret is not configured");
    }

    [Fact]
    public async Task Should_ReturnFail_WhenCookieFormatInvalid()
    {
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", "nodotinthiscookie");

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid session cookie format");
    }

    [Fact]
    public async Task Should_ReturnFail_WhenSignatureInvalid()
    {
        var userId = Guid.NewGuid();
        var cookie = CreateSessionCookie(userId.ToString(), "admin@test.com", "SuperAdmin",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), TestSecret);

        // Replace signature with a completely invalid one
        var parts = cookie.Split('.');
        var tamperedCookie = $"{parts[0]}.{"0".PadLeft(parts[1].Length, '0')}";

        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", tamperedCookie);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid session signature");
    }

    [Fact]
    public async Task Should_ReturnSuccess_WhenValidCookieWithValidSignature()
    {
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "admin@test.com",
            DisplayName = "Test Admin",
            PasswordHash = "hash",
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var cookie = CreateSessionCookie(userId.ToString(), "admin@test.com", "SuperAdmin",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), TestSecret);
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", cookie);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
        result.Ticket!.AuthenticationScheme.Should().Be(AdminSessionAuthHandler.SchemeName);
    }

    [Fact]
    public async Task Should_ReturnFail_WhenSessionExpired()
    {
        var userId = Guid.NewGuid();
        var cookie = CreateSessionCookie(userId.ToString(), "admin@test.com", "SuperAdmin",
            DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(), TestSecret);
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", cookie);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Session expired");
    }

    [Fact]
    public async Task Should_ReturnFail_WhenUserIdInvalidGuid()
    {
        var cookie = CreateSessionCookie("not-a-guid", "admin@test.com", "SuperAdmin",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), TestSecret);
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", cookie);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid user ID");
    }

    [Fact]
    public async Task Should_ReturnNoResult_WhenAdminUserNotFound()
    {
        var userId = Guid.NewGuid();
        SetupDbWithAdminUsers(new List<AdminUser>());

        var cookie = CreateSessionCookie(userId.ToString(), "admin@test.com", "SuperAdmin",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), TestSecret);
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", cookie);

        var result = await AuthenticateAsync(httpContext);

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnNoResult_WhenAdminUserInactive()
    {
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "inactive@test.com",
            DisplayName = "Inactive Admin",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var cookie = CreateSessionCookie(userId.ToString(), "inactive@test.com", "Admin",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), TestSecret);
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", cookie);

        var result = await AuthenticateAsync(httpContext);

        result.None.Should().BeTrue();
    }

    private const string DefaultTestMethod = "GET";
    private const string DefaultTestPath = "/";

    private static string CreateProxyToken(
        string userId, long timestamp, string secret,
        string method = DefaultTestMethod, string path = DefaultTestPath)
    {
        var tsStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var encodedTs = Base64UrlEncode(Encoding.UTF8.GetBytes(tsStr));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signingInput = $"{ProxyTokenHmacDomain}{method.ToUpperInvariant()}\n{path}\n{userId}.{tsStr}";
        var sig = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput))).ToLowerInvariant();
        return $"{encodedTs}.{sig}";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    [Fact]
    public async Task Should_RejectUnsignedAdminUserIdHeader_WhenNoProxyToken()
    {
        // C1 auth bypass guard: an unsigned X-Admin-User-Id must NEVER authenticate.
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "proxy@test.com",
            DisplayName = "Proxy Admin",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Admin-User-Id"] = userId.ToString();
        // Deliberately NO X-Admin-Proxy-Token header.

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue(
            "unsigned admin headers must be ignored entirely — no impersonation permitted");
    }

    [Fact]
    public async Task Should_AuthenticateViaProxyToken_WhenSignatureValidAndFresh()
    {
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "proxy@test.com",
            DisplayName = "Proxy Admin",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = CreateProxyToken(userId.ToString(), now, TestSecret);

        var httpContext = BuildProxyContext(userId.ToString(), token);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
        result.Ticket!.Principal.FindFirst("AdminUserId")!.Value.Should().Be(userId.ToString());
    }

    private static DefaultHttpContext BuildProxyContext(
        string userId, string? token, string method = DefaultTestMethod, string path = DefaultTestPath)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Request.Headers["X-Admin-User-Id"] = userId;
        if (token is not null) ctx.Request.Headers["X-Admin-Proxy-Token"] = token;
        return ctx;
    }

    [Fact]
    public async Task Should_RejectProxyToken_WhenExpired()
    {
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "proxy@test.com",
            DisplayName = "Proxy Admin",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        // Well beyond the 60s max-age window
        var expired = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var token = CreateProxyToken(userId.ToString(), expired, TestSecret);

        var httpContext = BuildProxyContext(userId.ToString(), token);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Should_RejectProxyToken_WhenTampered()
    {
        var userId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = attackerId,
            Email = "attacker@test.com",
            DisplayName = "Attacker",
            PasswordHash = "hash",
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        // Token legitimately signed for `userId`, but attacker swaps in a different user id
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = CreateProxyToken(userId.ToString(), now, TestSecret);

        var httpContext = BuildProxyContext(attackerId.ToString(), token);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("signature");
    }

    [Fact]
    public async Task Should_RejectProxyToken_WhenMethodDoesNotMatchSignedInput()
    {
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "proxy@test.com",
            DisplayName = "Proxy Admin",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // Token signed for GET, replayed as DELETE
        var token = CreateProxyToken(userId.ToString(), now, TestSecret, method: "GET", path: "/api/v1/admin/tenants");

        var httpContext = BuildProxyContext(
            userId.ToString(), token, method: "DELETE", path: "/api/v1/admin/tenants");

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("signature");
    }

    [Fact]
    public async Task Should_RejectProxyToken_WhenPathDoesNotMatchSignedInput()
    {
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "proxy@test.com",
            DisplayName = "Proxy Admin",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = CreateProxyToken(userId.ToString(), now, TestSecret,
            method: "POST", path: "/api/v1/admin/tenants");

        var httpContext = BuildProxyContext(
            userId.ToString(), token, method: "POST", path: "/api/v1/admin/users/delete");

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("signature");
    }

    [Fact]
    public async Task Should_RejectProxyToken_WhenTimestampIsZero()
    {
        var userId = Guid.NewGuid();
        SetupDbWithAdminUsers(new List<AdminUser>());

        var token = CreateProxyToken(userId.ToString(), 0L, TestSecret);
        var httpContext = BuildProxyContext(userId.ToString(), token);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("timestamp");
    }

    [Fact]
    public async Task Should_AcceptUnsignedHeader_WhenRequireProxyTokenFlagIsFalse()
    {
        // Rollout escape hatch: when the operator explicitly flips the flag to false,
        // the legacy unsigned header path works (with a loud warning log emitted).
        _options.RequireProxyToken = false;

        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "legacy@test.com",
            DisplayName = "Legacy Admin",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var httpContext = BuildProxyContext(userId.ToString(), token: null);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AdminEndpoint_With_OnlyXAdminUserIdHeader_Returns401Equivalent()
    {
        // Integration-style: a request to a realistic admin route carrying ONLY
        // the unsigned X-Admin-User-Id header must not authenticate. The authn
        // middleware maps a non-successful authenticate result for an
        // [Authorize] endpoint to 401 — so an unauthenticated AuthenticateResult
        // here is the direct cause of the 401 at the pipeline level.
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "bypass@test.com",
            DisplayName = "Bypass Attempt",
            PasswordHash = "hash",
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var httpContext = BuildProxyContext(
            userId.ToString(), token: null,
            method: "DELETE", path: "/api/v1/admin/tenants/123");

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse(
            "an unsigned admin header must never produce a successful authentication ticket");
        result.Principal.Should().BeNull();
    }

    [Fact]
    public async Task Should_RejectUnsignedHeader_WhenRequireProxyTokenFlagIsTrue()
    {
        // Default / secure posture: unsigned headers yield NoResult (no auth).
        _options.RequireProxyToken = true;

        var userId = Guid.NewGuid();
        var httpContext = BuildProxyContext(userId.ToString(), token: null);

        var result = await AuthenticateAsync(httpContext);

        result.None.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // Chunk B/C: metric + EnforceAfter behaviour + startup fail-fast.
    // ---------------------------------------------------------------------

    private sealed class MetricCollector : IDisposable
    {
        private readonly MeterListener _listener;
        public List<(long Value, string Outcome)> Measurements { get; } = new();

        public MetricCollector()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AdminSessionAuthHandler.MeterName
                        && instrument.Name == AdminSessionAuthHandler.ProxyTokenMissingCounterName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                string outcome = "";
                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome") outcome = tag.Value?.ToString() ?? "";
                }
                lock (Measurements) Measurements.Add((measurement, outcome));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    [Fact]
    public async Task Metric_ProxyTokenMissing_Rejected_Increments_WhenRequireProxyTokenTrue()
    {
        _options.RequireProxyToken = true;
        _options.EnforceAfter = null;

        using var collector = new MetricCollector();

        var userId = Guid.NewGuid();
        var httpContext = BuildProxyContext(userId.ToString(), token: null);

        var result = await AuthenticateAsync(httpContext);

        result.None.Should().BeTrue();
        collector.Measurements.Should().ContainSingle()
            .Which.Outcome.Should().Be("rejected");
    }

    [Fact]
    public async Task Metric_ProxyTokenMissing_AllowedDuringGrace_Increments_WhenFlagFalse()
    {
        _options.RequireProxyToken = false;
        _options.EnforceAfter = null;

        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "legacy-metric@test.com",
            DisplayName = "Legacy",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        using var collector = new MetricCollector();

        var httpContext = BuildProxyContext(userId.ToString(), token: null);
        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
        collector.Measurements.Should().ContainSingle()
            .Which.Outcome.Should().Be("allowed_during_grace");
    }

    [Fact]
    public async Task EnforceAfter_BeforeCutover_AllowsLegacyUnsignedHeader_EvenWithRequireProxyTokenTrue()
    {
        // Grace window: the operator has staged a cut-over but we are still before it.
        // The legacy unsigned header MUST work so existing traffic does not break mid-flight.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2026-04-14T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        _options.RequireProxyToken = true;
        _options.EnforceAfter = DateTimeOffset.Parse("2026-04-14T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture); // 2h in future

        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "grace@test.com",
            DisplayName = "Grace",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        using var collector = new MetricCollector();

        var httpContext = BuildProxyContext(userId.ToString(), token: null);
        var result = await AuthenticateAsync(httpContext, fakeTime);

        result.Succeeded.Should().BeTrue("EnforceAfter grace window still permits legacy header");
        collector.Measurements.Should().ContainSingle()
            .Which.Outcome.Should().Be("allowed_during_grace");
    }

    [Fact]
    public async Task EnforceAfter_AfterCutover_RejectsUnsignedHeader_EvenWithRequireProxyTokenFalse()
    {
        // Past the cut-over instant, the legacy unsigned path is ALWAYS rejected —
        // the RequireProxyToken=false escape hatch is time-bound and cannot be
        // left open indefinitely.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2026-04-14T14:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        _options.RequireProxyToken = false;
        _options.EnforceAfter = DateTimeOffset.Parse("2026-04-14T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture); // 2h in the past

        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "past-cutover@test.com",
            DisplayName = "Past",
            PasswordHash = "hash",
            Role = AdminRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        using var collector = new MetricCollector();

        var httpContext = BuildProxyContext(userId.ToString(), token: null);
        var result = await AuthenticateAsync(httpContext, fakeTime);

        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
        collector.Measurements.Should().ContainSingle()
            .Which.Outcome.Should().Be("rejected");
    }

    [Fact]
    public void Startup_FailsFast_WhenRequireProxyTokenTrue_AndSecretTooShort()
    {
        // Fewer than 32 bytes must fail at startup when the signed-token contract is on.
        var configValues = new Dictionary<string, string?>
        {
            ["Authentication:AdminSession:SessionSecret"] = "tiny",
            ["Authentication:AdminSession:RequireProxyToken"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));

        services.AddApiServices(config);

        var provider = services.BuildServiceProvider();

        // Options are materialised lazily; the handler-config callback runs when
        // the auth scheme is built. Forcing options resolution triggers the guard.
        var act = () =>
        {
            var monitor = provider.GetRequiredService<IOptionsMonitor<AdminSessionAuthSchemeOptions>>();
            _ = monitor.Get(AdminSessionAuthHandler.SchemeName);
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32 bytes*");
    }

    [Fact]
    public void Startup_Succeeds_WhenRequireProxyTokenTrue_AndSecretStrong()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Authentication:AdminSession:SessionSecret"] = new string('k', 48),
            ["Authentication:AdminSession:RequireProxyToken"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));

        services.AddApiServices(config);
        var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IOptionsMonitor<AdminSessionAuthSchemeOptions>>();
        var opts = monitor.Get(AdminSessionAuthHandler.SchemeName);

        opts.SessionSecret.Length.Should().BeGreaterThanOrEqualTo(32);
        opts.RequireProxyToken.Should().BeTrue();
    }

    [Fact]
    public void ToString_Redacts_SessionSecret()
    {
        var opts = new AdminSessionAuthSchemeOptions
        {
            SessionSecret = "super-secret-value-do-not-leak",
            RequireProxyToken = true
        };

        var rendered = opts.ToString();

        rendered.Should().NotContain("super-secret-value-do-not-leak");
        rendered.Should().Contain("SessionSecret=***");
    }

    [Fact]
    public async Task Should_SetCorrectClaims_AdminUserId_Email_Role()
    {
        var userId = Guid.NewGuid();
        var adminUser = new AdminUser
        {
            Id = userId,
            Email = "claims@test.com",
            DisplayName = "Claims Admin",
            PasswordHash = "hash",
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupDbWithAdminUsers(new List<AdminUser> { adminUser });

        var cookie = CreateSessionCookie(userId.ToString(), "claims@test.com", "SuperAdmin",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), TestSecret);
        var httpContext = CreateHttpContextWithCookie("eaas_admin_session", cookie);

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();

        var principal = result.Ticket!.Principal;
        principal.FindFirst("AdminUserId")!.Value.Should().Be(userId.ToString());
        principal.FindFirst("AdminEmail")!.Value.Should().Be("claims@test.com");
        principal.FindFirst("AdminRole")!.Value.Should().Be(AdminRole.SuperAdmin.ToString());
        principal.Identity!.AuthenticationType.Should().Be(AdminSessionAuthHandler.SchemeName);
    }
}
