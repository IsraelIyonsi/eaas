using EaaS.Domain.Exceptions;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.ApiKeys;

public sealed partial class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly IApiKeyCache _apiKeyCache;
    private readonly ILogger<RevokeApiKeyHandler> _logger;

    public RevokeApiKeyHandler(AppDbContext dbContext, IApiKeyCache apiKeyCache, ILogger<RevokeApiKeyHandler> logger)
    {
        _dbContext = dbContext;
        _apiKeyCache = apiKeyCache;
        _logger = logger;
    }

    public async Task Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await _dbContext.ApiKeys
            .Where(k => k.Id == request.Id && k.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"API key with id '{request.Id}' not found.");

        apiKey.Status = ApiKeyStatus.Revoked;
        apiKey.RevokedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache for this key
        await _apiKeyCache.InvalidateApiKeyCacheAsync(apiKey.KeyHash, cancellationToken);

        LogApiKeyRevoked(_logger, request.Id, request.TenantId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "API key revoked: ApiKeyId={ApiKeyId}, TenantId={TenantId}")]
    private static partial void LogApiKeyRevoked(ILogger logger, Guid apiKeyId, Guid tenantId);
}
