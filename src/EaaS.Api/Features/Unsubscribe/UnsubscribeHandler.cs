using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Unsubscribe;

public sealed partial class UnsubscribeHandler : IRequestHandler<UnsubscribeCommand, UnsubscribeResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ListUnsubscribeService _tokenService;
    private readonly ILogger<UnsubscribeHandler> _logger;

    public UnsubscribeHandler(
        AppDbContext dbContext,
        ListUnsubscribeService tokenService,
        ILogger<UnsubscribeHandler> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<UnsubscribeResult> Handle(UnsubscribeCommand request, CancellationToken cancellationToken)
    {
        var data = _tokenService.ValidateToken(request.Token);
        if (data is null)
        {
            LogInvalidToken(_logger);
            return new UnsubscribeResult(false, null, null);
        }

        var normalized = data.RecipientEmail.Trim().ToLowerInvariant();

        // Idempotent — if already suppressed, just return success.
        var existing = await _dbContext.SuppressionEntries
            .FirstOrDefaultAsync(
                s => s.TenantId == data.TenantId && s.EmailAddress == normalized,
                cancellationToken);

        if (existing is not null)
        {
            LogAlreadySuppressed(_logger, data.TenantId, normalized);
            return new UnsubscribeResult(true, normalized, data.TenantId);
        }

        var now = DateTime.UtcNow;
        _dbContext.SuppressionEntries.Add(new SuppressionEntry
        {
            Id = Guid.NewGuid(),
            TenantId = data.TenantId,
            EmailAddress = normalized,
            Reason = SuppressionReason.Manual,
            CreatedAt = now,
            SuppressedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        LogUnsubscribed(_logger, data.TenantId, normalized);

        return new UnsubscribeResult(true, normalized, data.TenantId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid unsubscribe token received")]
    private static partial void LogInvalidToken(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recipient {Email} already suppressed for tenant {TenantId}")]
    private static partial void LogAlreadySuppressed(ILogger logger, Guid tenantId, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recipient {Email} unsubscribed from tenant {TenantId} via one-click")]
    private static partial void LogUnsubscribed(ILogger logger, Guid tenantId, string email);
}
