using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using EaaS.WebhookProcessor.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.WebhookProcessor.Handlers;

public sealed partial class BounceHandler
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BounceHandler> _logger;

    public BounceHandler(AppDbContext dbContext, ICacheService cacheService, IPublishEndpoint publishEndpoint, ILogger<BounceHandler> logger)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task HandleAsync(SesNotification notification, CancellationToken cancellationToken)
    {
        var bounce = notification.Bounce;
        if (bounce is null)
        {
            LogNoBounceData(_logger, notification.Mail.MessageId);
            return;
        }

        var email = await _dbContext.Emails
            .FirstOrDefaultAsync(e => e.SesMessageId == notification.Mail.MessageId, cancellationToken);

        if (email is null)
        {
            LogEmailNotFound(_logger, notification.Mail.MessageId);
            return;
        }

        var isPermanent = bounce.BounceType.Equals("Permanent", StringComparison.OrdinalIgnoreCase);
        LogBounceReceived(_logger, bounce.BounceType, bounce.BounceSubType, email.Id);

        if (isPermanent)
        {
            // Update email status to Bounced
            email.Status = EmailStatus.Bounced;

            // Add bounce event
            _dbContext.EmailEvents.Add(new EmailEvent
            {
                Id = Guid.NewGuid(),
                EmailId = email.Id,
                EventType = EventType.Bounced,
                Data = JsonSerializer.Serialize(new
                {
                    bounceType = bounce.BounceType,
                    bounceSubType = bounce.BounceSubType,
                    feedbackId = bounce.FeedbackId,
                    timestamp = bounce.Timestamp,
                    recipients = bounce.BouncedRecipients.Select(r => new
                    {
                        email = r.EmailAddress,
                        status = r.Status,
                        diagnosticCode = r.DiagnosticCode
                    })
                }),
                CreatedAt = DateTime.UtcNow
            });

            // Auto-suppress all bounced recipients
            foreach (var recipient in bounce.BouncedRecipients)
            {
                await SuppressRecipient(
                    email.TenantId,
                    recipient.EmailAddress,
                    SuppressionReason.HardBounce,
                    email.MessageId,
                    cancellationToken);
            }
        }
        else
        {
            // Transient bounce — log only, do NOT suppress
            _dbContext.EmailEvents.Add(new EmailEvent
            {
                Id = Guid.NewGuid(),
                EmailId = email.Id,
                EventType = EventType.Bounced,
                Data = JsonSerializer.Serialize(new
                {
                    bounceType = bounce.BounceType,
                    bounceSubType = bounce.BounceSubType,
                    feedbackId = bounce.FeedbackId,
                    timestamp = bounce.Timestamp,
                    transient = true,
                    recipients = bounce.BouncedRecipients.Select(r => new
                    {
                        email = r.EmailAddress,
                        status = r.Status,
                        diagnosticCode = r.DiagnosticCode
                    })
                }),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish webhook dispatch message
        await _publishEndpoint.Publish(new WebhookDispatchMessage
        {
            TenantId = email.TenantId,
            EventType = "bounced",
            EmailId = email.Id,
            MessageId = email.MessageId,
            Data = JsonSerializer.Serialize(new
            {
                bounceType = bounce.BounceType,
                bounceSubType = bounce.BounceSubType,
                recipients = bounce.BouncedRecipients.Select(r => r.EmailAddress)
            }),
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task SuppressRecipient(
        Guid tenantId,
        string emailAddress,
        SuppressionReason reason,
        string sourceMessageId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = emailAddress.ToLowerInvariant();

        // Check if already suppressed
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

        // Update Redis cache regardless
        await _cacheService.AddToSuppressionCacheAsync(tenantId, normalizedEmail, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bounce notification has no bounce data for SES message {MessageId}")]
    private static partial void LogNoBounceData(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email not found for SES message {MessageId}")]
    private static partial void LogEmailNotFound(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bounce received: Type={BounceType}, SubType={BounceSubType}, EmailId={EmailId}")]
    private static partial void LogBounceReceived(ILogger logger, string bounceType, string bounceSubType, Guid emailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recipient {Email} suppressed due to {Reason}")]
    private static partial void LogRecipientSuppressed(ILogger logger, string email, string reason);
}
