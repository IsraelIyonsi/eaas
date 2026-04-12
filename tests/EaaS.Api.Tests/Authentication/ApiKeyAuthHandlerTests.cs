using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EaaS.Api.Authentication;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
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

public sealed class ApiKeyAuthHandlerTests
{
    private readonly AppDbContext _dbContext;
    private readonly IApiKeyCache _apiKeyCache;
    private readonly IOptionsMonitor<ApiKeyAuthSchemeOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;

    public ApiKeyAuthHandlerTests()
    {
        _dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        _apiKeyCache = Substitute.For<IApiKeyCache>();

        var options = new ApiKeyAuthSchemeOptions();
        _optionsMonitor = Substitute.For<IOptionsMonitor<ApiKeyAuthSchemeOptions>>();
        _optionsMonitor.Get(Arg.Any<string>()).Returns(options);
        _optionsMonitor.CurrentValue.Returns(options);

        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
    }

    private async Task<AuthenticateResult> AuthenticateAsync(HttpContext httpContext)
    {
        var handler = new ApiKeyAuthHandler(
            _optionsMonitor,
            _loggerFactory,
            UrlEncoder.Default,
            _dbContext,
            _apiKeyCache);

        var scheme = new AuthenticationScheme(ApiKeyAuthHandler.SchemeName, null, typeof(ApiKeyAuthHandler));
        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    private static string ComputeSha256Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void SetupDbWithApiKeys(List<ApiKey> apiKeys)
    {
        var mockSet = MockDbSetFactory.Create(apiKeys);
        _dbContext.ApiKeys.Returns(mockSet);
    }

    [Fact]
    public async Task Should_ReturnNoResult_WhenNoAuthorizationHeader()
    {
        var httpContext = new DefaultHttpContext();

        var result = await AuthenticateAsync(httpContext);

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnNoResult_WhenNotBearerScheme()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Basic dXNlcjpwYXNz";

        var result = await AuthenticateAsync(httpContext);

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFail_WhenApiKeyIsEmpty()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer   ";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("API key is empty");
    }

    [Fact]
    public async Task Should_ReturnSuccess_WhenApiKeyFoundInCache()
    {
        var tenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string rawKey = "test-api-key-cached";
        var keyHash = ComputeSha256Hash(rawKey);

        var cachedData = JsonSerializer.Serialize(new { TenantId = tenantId, ApiKeyId = apiKeyId, Name = "Cached Key", IsServiceKey = false });
        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns(cachedData);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
        result.Ticket!.AuthenticationScheme.Should().Be(ApiKeyAuthHandler.SchemeName);
    }

    [Fact]
    public async Task Should_ReturnSuccess_WhenApiKeyFoundInDb_AndCachesIt()
    {
        var tenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string rawKey = "test-api-key-db";
        var keyHash = ComputeSha256Hash(rawKey);

        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns((string?)null);

        var apiKeys = new List<ApiKey>
        {
            new()
            {
                Id = apiKeyId,
                TenantId = tenantId,
                Name = "DB Key",
                KeyHash = keyHash,
                Prefix = "eaas_liv",
                Status = ApiKeyStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        };
        SetupDbWithApiKeys(apiKeys);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
        await _apiKeyCache.Received(1).SetApiKeyCacheAsync(
            keyHash,
            Arg.Any<string>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnFail_WhenApiKeyNotFoundInDbOrCache()
    {
        const string rawKey = "nonexistent-key";
        var keyHash = ComputeSha256Hash(rawKey);

        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns((string?)null);
        SetupDbWithApiKeys(new List<ApiKey>());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid or revoked API key");
    }

    [Fact]
    public async Task Should_ReturnSuccess_WhenApiKeyIsRotating_WithinGracePeriod()
    {
        var tenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string rawKey = "rotating-key-valid";
        var keyHash = ComputeSha256Hash(rawKey);

        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns((string?)null);

        var apiKeys = new List<ApiKey>
        {
            new()
            {
                Id = apiKeyId,
                TenantId = tenantId,
                Name = "Rotating Key",
                KeyHash = keyHash,
                Prefix = "eaas_liv",
                Status = ApiKeyStatus.Rotating,
                RotatingExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow
            }
        };
        SetupDbWithApiKeys(apiKeys);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFail_WhenApiKeyIsRotating_PastGracePeriod()
    {
        var tenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string rawKey = "rotating-key-expired";
        var keyHash = ComputeSha256Hash(rawKey);

        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns((string?)null);

        var apiKeys = new List<ApiKey>
        {
            new()
            {
                Id = apiKeyId,
                TenantId = tenantId,
                Name = "Expired Rotating Key",
                KeyHash = keyHash,
                Prefix = "eaas_liv",
                Status = ApiKeyStatus.Rotating,
                RotatingExpiresAt = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            }
        };
        SetupDbWithApiKeys(apiKeys);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid or revoked API key");
    }

    [Fact]
    public async Task Should_ReturnFail_WhenApiKeyIsRevoked()
    {
        var tenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string rawKey = "revoked-key";
        var keyHash = ComputeSha256Hash(rawKey);

        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns((string?)null);

        var apiKeys = new List<ApiKey>
        {
            new()
            {
                Id = apiKeyId,
                TenantId = tenantId,
                Name = "Revoked Key",
                KeyHash = keyHash,
                Prefix = "eaas_liv",
                Status = ApiKeyStatus.Revoked,
                RevokedAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow
            }
        };
        SetupDbWithApiKeys(apiKeys);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ImpersonateTenant_WhenServiceKey_AndXTenantIdHeader()
    {
        var serviceTenantId = Guid.NewGuid();
        var impersonatedTenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string rawKey = "service-key-impersonate";
        var keyHash = ComputeSha256Hash(rawKey);

        var cachedData = JsonSerializer.Serialize(new { TenantId = serviceTenantId, ApiKeyId = apiKeyId, Name = "Service Key", IsServiceKey = true });
        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns(cachedData);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";
        httpContext.Request.Headers["X-Tenant-Id"] = impersonatedTenantId.ToString();

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
        result.Ticket!.Principal.FindFirst("TenantId")!.Value.Should().Be(impersonatedTenantId.ToString());
    }

    [Fact]
    public async Task Should_NotImpersonate_WhenNonServiceKey_AndXTenantIdHeader()
    {
        var ownTenantId = Guid.NewGuid();
        var impersonatedTenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string rawKey = "regular-key-no-impersonate";
        var keyHash = ComputeSha256Hash(rawKey);

        var cachedData = JsonSerializer.Serialize(new { TenantId = ownTenantId, ApiKeyId = apiKeyId, Name = "Regular Key", IsServiceKey = false });
        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns(cachedData);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";
        httpContext.Request.Headers["X-Tenant-Id"] = impersonatedTenantId.ToString();

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();
        // Should use the key's own tenant, NOT the header value
        result.Ticket!.Principal.FindFirst("TenantId")!.Value.Should().Be(ownTenantId.ToString());
    }

    [Fact]
    public async Task Should_SetCorrectClaims_TenantId_ApiKeyId_Name()
    {
        var tenantId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        const string keyName = "Production Key";
        const string rawKey = "claims-test-key";
        var keyHash = ComputeSha256Hash(rawKey);

        var cachedData = JsonSerializer.Serialize(new { TenantId = tenantId, ApiKeyId = apiKeyId, Name = keyName, IsServiceKey = false });
        _apiKeyCache.GetApiKeyCacheAsync(keyHash, Arg.Any<CancellationToken>()).Returns(cachedData);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {rawKey}";

        var result = await AuthenticateAsync(httpContext);

        result.Succeeded.Should().BeTrue();

        var principal = result.Ticket!.Principal;
        principal.FindFirst("TenantId")!.Value.Should().Be(tenantId.ToString());
        principal.FindFirst("ApiKeyId")!.Value.Should().Be(apiKeyId.ToString());
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be(keyName);
        principal.Identity!.AuthenticationType.Should().Be(ApiKeyAuthHandler.SchemeName);
    }
}
