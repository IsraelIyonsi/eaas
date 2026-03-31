using EaaS.Domain.Exceptions;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Suppressions;

public sealed class AddSuppressionHandler : IRequestHandler<AddSuppressionCommand, AddSuppressionResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ISuppressionCache _suppressionCache;

    public AddSuppressionHandler(AppDbContext dbContext, ISuppressionCache suppressionCache)
    {
        _dbContext = dbContext;
        _suppressionCache = suppressionCache;
    }

    public async Task<AddSuppressionResult> Handle(AddSuppressionCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.EmailAddress.ToLowerInvariant();

        var exists = await _dbContext.SuppressionEntries
            .AsNoTracking()
            .AnyAsync(s => s.TenantId == request.TenantId && s.EmailAddress == emailLower, cancellationToken);

        if (exists)
            throw new ConflictException($"Email address '{request.EmailAddress}' is already suppressed.");

        var entry = new SuppressionEntry
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            EmailAddress = emailLower,
            Reason = SuppressionReason.Manual,
            SuppressedAt = DateTime.UtcNow
        };

        _dbContext.SuppressionEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Update Redis cache
        await _suppressionCache.AddToSuppressionCacheAsync(request.TenantId, emailLower, cancellationToken);

        return new AddSuppressionResult(
            entry.Id,
            entry.EmailAddress,
            entry.Reason.ToString().ToLowerInvariant(),
            entry.SuppressedAt);
    }
}
