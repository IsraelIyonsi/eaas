using EaaS.Domain.Exceptions;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.ApiKeys;

public sealed class RotateApiKeyHandler : IRequestHandler<RotateApiKeyCommand, RotateApiKeyResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IApiKeyCache _apiKeyCache;

    public RotateApiKeyHandler(AppDbContext dbContext, IApiKeyCache apiKeyCache)
    {
        _dbContext = dbContext;
        _apiKeyCache = apiKeyCache;
    }

    public async Task<RotateApiKeyResult> Handle(RotateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var existingKey = await _dbContext.ApiKeys
            .Where(k => k.Id == request.Id && k.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"API key with id '{request.Id}' not found.");

        if (existingKey.Status != ApiKeyStatus.Active)
            throw new ConflictException("Only active API keys can be rotated.");

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
        await _apiKeyCache.SetApiKeyCacheAsync(keyHash, newCacheData, cancellationToken: cancellationToken);

        // Keep old key cached with 24h TTL
        var oldCacheData = await _apiKeyCache.GetApiKeyCacheAsync(existingKey.KeyHash, cancellationToken);
        if (oldCacheData is not null)
        {
            await _apiKeyCache.SetApiKeyCacheAsync(existingKey.KeyHash, oldCacheData, TimeSpan.FromHours(ApiKeyConstants.GracePeriodHours), cancellationToken);
        }

        return new RotateApiKeyResult(
            newApiKey.Id,
            plaintextKey,
            prefix,
            gracePeriodExpiry,
            newApiKey.CreatedAt);
    }

    private static string GenerateApiKey() => ApiKeyGenerator.GenerateKey();

    private static string ComputeSha256Hash(string rawKey) => ApiKeyGenerator.ComputeSha256Hash(rawKey);
}
