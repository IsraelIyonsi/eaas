using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Suppressions;

public sealed class RemoveSuppressionHandler : IRequestHandler<RemoveSuppressionCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public RemoveSuppressionHandler(AppDbContext dbContext, ICacheService cacheService)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task Handle(RemoveSuppressionCommand request, CancellationToken cancellationToken)
    {
        var entry = await _dbContext.SuppressionEntries
            .Where(s => s.Id == request.Id && s.TenantId == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EaaS.Domain.Exceptions.NotFoundException($"Suppression entry with id '{request.Id}' not found.");

        _dbContext.SuppressionEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Remove from Redis cache
        await _cacheService.RemoveFromSuppressionCacheAsync(request.TenantId, entry.EmailAddress, cancellationToken);
    }
}
