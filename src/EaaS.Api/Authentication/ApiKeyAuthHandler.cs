using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EaaS.Api.Constants;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EaaS.Api.Authentication;

public sealed partial class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly AppDbContext _dbContext;
    private readonly IApiKeyCache _apiKeyCache;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext dbContext,
        IApiKeyCache apiKeyCache)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
        _apiKeyCache = apiKeyCache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HttpHeaderConstants.Authorization, out var authHeader))
        {
            LogMissingAuthHeader(Logger);
            return AuthenticateResult.NoResult();
        }

        var headerValue = authHeader.ToString();

        if (!headerValue.StartsWith(HttpHeaderConstants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var apiKey = headerValue[HttpHeaderConstants.BearerPrefix.Length..].Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.Fail("API key is empty.");

        var keyHash = ComputeSha256Hash(apiKey);

        // Check Redis cache first
        var cached = await _apiKeyCache.GetApiKeyCacheAsync(keyHash);
        CachedApiKeyData? keyData = null;
        if (cached is not null)
        {
            keyData = JsonSerializer.Deserialize<CachedApiKeyData>(cached);
        }

        if (keyData is null)
        {
            // Look up in DB - Active or Rotating (within grace period)
            var dbKey = await _dbContext.ApiKeys
                .AsNoTracking()
                .Where(k => k.KeyHash == keyHash
                            && (k.Status == ApiKeyStatus.Active
                                || (k.Status == ApiKeyStatus.Rotating && k.RotatingExpiresAt > DateTime.UtcNow)))
                .Select(k => new { k.Id, k.TenantId, k.Name, k.IsServiceKey })
                .FirstOrDefaultAsync();

            if (dbKey is null)
            {
                LogInvalidApiKey(Logger, Request.Path);
                return AuthenticateResult.Fail("Invalid or revoked API key.");
            }

            // Cache the result
            keyData = new CachedApiKeyData(dbKey.TenantId, dbKey.Id, dbKey.Name, dbKey.IsServiceKey);
            await _apiKeyCache.SetApiKeyCacheAsync(keyHash, JsonSerializer.Serialize(keyData));
        }

        var effectiveTenantId = keyData.TenantId;

        // Service key impersonation: dashboard proxy sends X-Tenant-Id to act on behalf of a tenant
        if (keyData.IsServiceKey
            && Request.Headers.TryGetValue(HttpHeaderConstants.TenantId, out var tenantIdHeader)
            && Guid.TryParse(tenantIdHeader.ToString(), out var impersonatedTenantId))
        {
            effectiveTenantId = impersonatedTenantId;
            LogServiceKeyImpersonation(Logger, keyData.ApiKeyId, impersonatedTenantId, Request.Path);
        }

        LogApiKeyAuthenticated(Logger, keyData.ApiKeyId, effectiveTenantId, Request.Path);

        var principal = BuildClaimsPrincipal(effectiveTenantId, keyData.ApiKeyId, keyData.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(Guid tenantId, Guid apiKeyId, string name)
    {
        var claims = new[]
        {
            new Claim(ClaimNameConstants.TenantId, tenantId.ToString()),
            new Claim(ClaimNameConstants.ApiKeyId, apiKeyId.ToString()),
            new Claim(ClaimTypes.Name, name)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        return new ClaimsPrincipal(identity);
    }

    private static string ComputeSha256Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record CachedApiKeyData(Guid TenantId, Guid ApiKeyId, string Name, bool IsServiceKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No Authorization header present on request")]
    private static partial void LogMissingAuthHeader(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid or revoked API key used on {RequestPath}")]
    private static partial void LogInvalidApiKey(ILogger logger, string requestPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "API key {ApiKeyId} authenticated for TenantId={TenantId} on {RequestPath}")]
    private static partial void LogApiKeyAuthenticated(ILogger logger, Guid apiKeyId, Guid tenantId, string requestPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service key {ApiKeyId} impersonating TenantId={TenantId} on {RequestPath}")]
    private static partial void LogServiceKeyImpersonation(ILogger logger, Guid apiKeyId, Guid tenantId, string requestPath);
}
