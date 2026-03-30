using System.Security.Cryptography;
using System.Text;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.ApiKeys;

public sealed class RotateApiKeyHandler : IRequestHandler<RotateApiKeyCommand, RotateApiKeyResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public RotateApiKeyHandler(AppDbContext dbContext, ICacheService cacheService)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task<RotateApiKeyResult> Handle(RotateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var existingKey = await _dbContext.ApiKeys
            .Where(k => k.Id == request.Id && k.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"API key with id '{request.Id}' not found.");

        if (existingKey.Status != ApiKeyStatus.Active)
            throw new InvalidOperationException("Only active API keys can be rotated.");

        // Generate new key
        var plaintextKey = GenerateApiKey();
        var keyHash = ComputeSha256Hash(plaintextKey);
        var prefix = plaintextKey[..8];
        var gracePeriodExpiry = DateTime.UtcNow.AddHours(ApiKeyConstants.GracePeriodHours);

        // Update old key to Rotating status
        existingKey.Status = ApiKeyStatus.Rotating;
        existingKey.RotatingExpiresAt = gracePeriodExpiry;

        // Create new API key
        var newApiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = existingKey.Name + " (rotated)",
            KeyHash = keyHash,
            Prefix = prefix,
            AllowedDomains = existingKey.AllowedDomains,
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Set audit trail
        existingKey.ReplacedByKeyId = newApiKey.Id;

        _dbContext.ApiKeys.Add(newApiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Cache the new key
        var newCacheData = System.Text.Json.JsonSerializer.Serialize(
            new { TenantId = newApiKey.TenantId, ApiKeyId = newApiKey.Id, Name = newApiKey.Name });
        await _cacheService.SetApiKeyCacheAsync(keyHash, newCacheData, cancellationToken: cancellationToken);

        // Keep old key cached with 24h TTL
        var oldCacheData = await _cacheService.GetApiKeyCacheAsync(existingKey.KeyHash, cancellationToken);
        if (oldCacheData is not null)
        {
            await _cacheService.SetApiKeyCacheAsync(existingKey.KeyHash, oldCacheData, TimeSpan.FromHours(ApiKeyConstants.GracePeriodHours), cancellationToken);
        }

        return new RotateApiKeyResult(
            newApiKey.Id,
            plaintextKey,
            prefix,
            gracePeriodExpiry,
            newApiKey.CreatedAt);
    }

    private static string GenerateApiKey()
    {
        var random = new char[ApiKeyConstants.RandomPartLength];

        for (var i = 0; i < ApiKeyConstants.RandomPartLength; i++)
            random[i] = ApiKeyConstants.AllowedCharacters[RandomNumberGenerator.GetInt32(ApiKeyConstants.AllowedCharacters.Length)];

        return ApiKeyConstants.LiveKeyPrefix + new string(random);
    }

    private static string ComputeSha256Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
