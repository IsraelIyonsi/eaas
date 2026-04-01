using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.WebhookProcessor.Services;

public sealed partial class RecipientSuppressor
{
    private readonly AppDbContext _dbContext;
    private readonly ISuppressionCache _suppressionCache;
    private readonly ILogger<RecipientSuppressor> _logger;

    public RecipientSuppressor(AppDbContext dbContext, ISuppressionCache suppressionCache, ILogger<RecipientSuppressor> logger)
    {
        _dbContext = dbContext;
        _suppressionCache = suppressionCache;
        _logger = logger;
    }

    public async Task SuppressAsync(
        Guid tenantId,
        string emailAddress,
        SuppressionReason reason,
        string sourceMessageId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = emailAddress.ToLowerInvariant();

        var existing = await _dbContext.SuppressionEntries
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.EmailAddress == normalizedEmail,
                cancellationToken);

        if (existing is null)
        {
            _dbContext.SuppressionEntries.Add(new SuppressionEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmailAddress = normalizedEmail,
                Reason = reason,
                SourceMessageId = sourceMessageId,
                SuppressedAt = DateTime.UtcNow
            });

            LogRecipientSuppressed(_logger, normalizedEmail, reason.ToString());
        }

        await _suppressionCache.AddToSuppressionCacheAsync(tenantId, normalizedEmail, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recipient {Email} suppressed due to {Reason}")]
    private static partial void LogRecipientSuppressed(ILogger logger, string email, string reason);
}
