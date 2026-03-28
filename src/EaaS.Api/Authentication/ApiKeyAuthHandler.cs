using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EaaS.Api.Authentication;

public sealed class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext dbContext,
        ICacheService cacheService)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return AuthenticateResult.NoResult();

        var headerValue = authHeader.ToString();

        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var apiKey = headerValue["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.Fail("API key is empty.");

        var keyHash = ComputeSha256Hash(apiKey);

        // Check Redis cache first
        var cached = await _cacheService.GetApiKeyCacheAsync(keyHash);
        if (cached is not null)
        {
            var cachedData = JsonSerializer.Deserialize<CachedApiKeyData>(cached);
            if (cachedData is not null)
            {
                var cachedPrincipal = BuildClaimsPrincipal(cachedData.TenantId, cachedData.ApiKeyId, cachedData.Name);
                return AuthenticateResult.Success(new AuthenticationTicket(cachedPrincipal, SchemeName));
            }
        }

        // Look up in DB - Active or Rotating (within grace period)
        var dbKey = await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.KeyHash == keyHash
                        && (k.Status == ApiKeyStatus.Active
                            || (k.Status == ApiKeyStatus.Rotating && k.RotatingExpiresAt > DateTime.UtcNow)))
            .Select(k => new { k.Id, k.TenantId, k.Name })
            .FirstOrDefaultAsync();

        if (dbKey is null)
            return AuthenticateResult.Fail("Invalid or revoked API key.");

        // Cache the result
        var dataToCache = new CachedApiKeyData(dbKey.TenantId, dbKey.Id, dbKey.Name);
        await _cacheService.SetApiKeyCacheAsync(keyHash, JsonSerializer.Serialize(dataToCache));

        var principal = BuildClaimsPrincipal(dbKey.TenantId, dbKey.Id, dbKey.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(Guid tenantId, Guid apiKeyId, string name)
    {
        var claims = new[]
        {
            new Claim("TenantId", tenantId.ToString()),
            new Claim("ApiKeyId", apiKeyId.ToString()),
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

    private sealed record CachedApiKeyData(Guid TenantId, Guid ApiKeyId, string Name);
}
