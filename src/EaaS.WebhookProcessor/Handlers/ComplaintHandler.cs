using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using EaaS.WebhookProcessor.Models;
using EaaS.WebhookProcessor.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.WebhookProcessor.Handlers;

public sealed partial class ComplaintHandler
{
    private readonly AppDbContext _dbContext;
    private readonly RecipientSuppressor _suppressor;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ComplaintHandler> _logger;

    public ComplaintHandler(AppDbContext dbContext, RecipientSuppressor suppressor, IPublishEndpoint publishEndpoint, ILogger<ComplaintHandler> logger)
    {
        _dbContext = dbContext;
        _suppressor = suppressor;
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
        EmailMetrics.ComplaintsTotal.WithLabels(email.TenantId.ToString()).Inc();

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
            await _suppressor.SuppressAsync(
                email.TenantId,
                recipient.EmailAddress,
                SuppressionReason.Complaint,
                email.MessageId,
                cancellationToken);
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

}
