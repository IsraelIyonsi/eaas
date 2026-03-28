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

public sealed partial class ComplaintHandler
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ComplaintHandler> _logger;

    public ComplaintHandler(AppDbContext dbContext, ICacheService cacheService, IPublishEndpoint publishEndpoint, ILogger<ComplaintHandler> logger)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task HandleAsync(SesNotification notification, CancellationToken cancellationToken)
    {
        var complaint = notification.Complaint;
        if (complaint is null)
        {
            LogNoComplaintData(_logger, notification.Mail.MessageId);
            return;
        }

        var email = await _dbContext.Emails
            .FirstOrDefaultAsync(e => e.SesMessageId == notification.Mail.MessageId, cancellationToken);

        if (email is null)
        {
            LogEmailNotFound(_logger, notification.Mail.MessageId);
            return;
        }

        LogComplaintReceived(_logger, complaint.ComplaintFeedbackType ?? "unknown", email.Id);

        // Update email status
        email.Status = EmailStatus.Complained;

        // Add complaint event
        _dbContext.EmailEvents.Add(new EmailEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            EventType = EventType.Complained,
            Data = JsonSerializer.Serialize(new
            {
                complaintFeedbackType = complaint.ComplaintFeedbackType,
                feedbackId = complaint.FeedbackId,
                timestamp = complaint.Timestamp,
                recipients = complaint.ComplainedRecipients.Select(r => r.EmailAddress)
            }),
            CreatedAt = DateTime.UtcNow
        });

        // Auto-suppress all complained recipients
        foreach (var recipient in complaint.ComplainedRecipients)
        {
            var normalizedEmail = recipient.EmailAddress.ToLowerInvariant();

            var existing = await _dbContext.SuppressionEntries
                .FirstOrDefaultAsync(
                    s => s.TenantId == email.TenantId && s.EmailAddress == normalizedEmail,
                    cancellationToken);

            if (existing is null)
            {
                _dbContext.SuppressionEntries.Add(new SuppressionEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = email.TenantId,
                    EmailAddress = normalizedEmail,
                    Reason = SuppressionReason.Complaint,
                    SourceMessageId = email.MessageId,
                    SuppressedAt = DateTime.UtcNow
                });

                LogRecipientSuppressed(_logger, normalizedEmail);
            }

            await _cacheService.AddToSuppressionCacheAsync(email.TenantId, normalizedEmail, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish webhook dispatch message
        await _publishEndpoint.Publish(new WebhookDispatchMessage
        {
            TenantId = email.TenantId,
            EventType = "complained",
            EmailId = email.Id,
            MessageId = email.MessageId,
            Data = JsonSerializer.Serialize(new
            {
                complaintFeedbackType = complaint.ComplaintFeedbackType,
                recipients = complaint.ComplainedRecipients.Select(r => r.EmailAddress)
            }),
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Complaint notification has no complaint data for SES message {MessageId}")]
    private static partial void LogNoComplaintData(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email not found for SES message {MessageId}")]
    private static partial void LogEmailNotFound(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Complaint received: FeedbackType={FeedbackType}, EmailId={EmailId}")]
    private static partial void LogComplaintReceived(ILogger logger, string feedbackType, Guid emailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recipient {Email} suppressed due to complaint")]
    private static partial void LogRecipientSuppressed(ILogger logger, string email);
}
