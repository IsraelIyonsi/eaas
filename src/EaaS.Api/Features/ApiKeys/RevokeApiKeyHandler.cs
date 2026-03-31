using EaaS.Domain.Exceptions;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.ApiKeys;

public sealed class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly IApiKeyCache _apiKeyCache;

    public RevokeApiKeyHandler(AppDbContext dbContext, IApiKeyCache apiKeyCache)
    {
        _dbContext = dbContext;
        _apiKeyCache = apiKeyCache;
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
    }
}
