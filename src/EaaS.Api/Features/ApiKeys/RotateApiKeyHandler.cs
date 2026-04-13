using EaaS.Domain.Exceptions;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.ApiKeys;

public sealed partial class RotateApiKeyHandler : IRequestHandler<RotateApiKeyCommand, RotateApiKeyResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IApiKeyCache _apiKeyCache;
    private readonly ILogger<RotateApiKeyHandler> _logger;

    public RotateApiKeyHandler(AppDbContext dbContext, IApiKeyCache apiKeyCache, ILogger<RotateApiKeyHandler> logger)
    {
        _dbContext = dbContext;
        _apiKeyCache = apiKeyCache;
        _logger = logger;
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

        // Insert new key first to avoid FK violation on ReplacedByKeyId
        _dbContext.ApiKeys.Add(newApiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Now set the audit trail FK (new key exists in DB)
        existingKey.ReplacedByKeyId = newApiKey.Id;
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

        LogApiKeyRotated(_logger, request.Id, newApiKey.Id, request.TenantId, gracePeriodExpiry);

        return new RotateApiKeyResult(
            newApiKey.Id,
            plaintextKey,
            prefix,
            gracePeriodExpiry,
            newApiKey.CreatedAt);
    }

    private static string GenerateApiKey() => ApiKeyGenerator.GenerateKey();

    private static string ComputeSha256Hash(string rawKey) => ApiKeyGenerator.ComputeSha256Hash(rawKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "API key rotated: OldKeyId={OldKeyId}, NewKeyId={NewKeyId}, TenantId={TenantId}, GraceExpiry={GraceExpiry}")]
    private static partial void LogApiKeyRotated(ILogger logger, Guid oldKeyId, Guid newKeyId, Guid tenantId, DateTime graceExpiry);
}
