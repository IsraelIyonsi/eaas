using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Services;

public sealed class SuppressionChecker
{
    private readonly ISuppressionCache _suppressionCache;
    private readonly AppDbContext _dbContext;

    public SuppressionChecker(ISuppressionCache suppressionCache, AppDbContext dbContext)
    {
        _suppressionCache = suppressionCache;
        _dbContext = dbContext;
    }

    public async Task<string?> FindSuppressedRecipientAsync(
        Guid tenantId, IEnumerable<string> recipients, CancellationToken cancellationToken)
    {
        foreach (var recipient in recipients)
        {
            var isSuppressed = await _suppressionCache.IsEmailSuppressedAsync(
                tenantId, recipient, cancellationToken);

            if (!isSuppressed)
            {
                var recipientLower = recipient.ToLowerInvariant();
                isSuppressed = await _dbContext.SuppressionEntries
                    .AsNoTracking()
                    .AnyAsync(s => s.TenantId == tenantId
                                   && s.EmailAddress == recipientLower, cancellationToken);
            }

            if (isSuppressed)
                return recipient;
        }

        return null;
    }

    public async Task EnsureNoneSuppressedOrThrowAsync(
        Guid tenantId, IEnumerable<string> recipients, CancellationToken cancellationToken)
    {
        var suppressed = await FindSuppressedRecipientAsync(tenantId, recipients, cancellationToken);
        if (suppressed is not null)
            throw new RecipientSuppressedException($"Recipient '{suppressed}' is on the suppression list.");
    }
}
