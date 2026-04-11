using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EaaS.Api.Authentication;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    private async Task<AuthenticateResult> AuthenticateAsync(HttpContext httpContext)
    {
        var handler = new AdminSessionAuthHandler(
            _optionsMonitor,
            _loggerFactory,
            UrlEncoder.Default,
            _dbContext);

        var scheme = new AuthenticationScheme(AdminSessionAuthHandler.SchemeName, null, typeof(AdminSessionAuthHandler));
        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    private void SetupDbWithAdminUsers(List<AdminUser> users)
    {
        var mockSet = MockDbSetFactory.Create(users);
        _dbContext.AdminUsers.Returns(mockSet);
    }

    private static string CreateSessionCookie(string userId, string email, string role, long? expiresAt, string secret)
    {
        var payload = JsonSerializer.Serialize(new { UserId = userId, Email = email, Role = role, ExpiresAt = expiresAt });
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(encoded))).ToLowerInvariant();
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

        // Tamper with signature
        var parts = cookie.Split('.');
        var tamperedCookie = $"{parts[0]}.{"a" + parts[1][1..]}";

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

    [Fact]
    public async Task Should_ReturnSuccess_ViaProxyHeader_WhenNoCookie()
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

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Admin-User-Id"] = userId.ToString();

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
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
