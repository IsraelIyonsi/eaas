using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.ApiKeys;

public sealed class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public RevokeApiKeyHandler(AppDbContext dbContext, ICacheService cacheService)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await _dbContext.ApiKeys
            .Where(k => k.Id == request.Id && k.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EaaS.Domain.Exceptions.NotFoundException($"API key with id '{request.Id}' not found.");

        apiKey.Status = ApiKeyStatus.Revoked;
        apiKey.RevokedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache for this key
        await _cacheService.InvalidateApiKeyCacheAsync(apiKey.KeyHash, cancellationToken);
    }
}
