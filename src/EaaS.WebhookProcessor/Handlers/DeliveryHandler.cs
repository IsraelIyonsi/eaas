using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using EaaS.WebhookProcessor.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.WebhookProcessor.Handlers;

public interface IDeliveryHandler
{
    Task HandleAsync(SesNotification notification, CancellationToken cancellationToken);
}

public sealed partial class DeliveryHandler : IDeliveryHandler
{
    private readonly AppDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<DeliveryHandler> _logger;

    public DeliveryHandler(AppDbContext dbContext, IPublishEndpoint publishEndpoint, ILogger<DeliveryHandler> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task HandleAsync(SesNotification notification, CancellationToken cancellationToken)
    {
        var delivery = notification.Delivery;
        if (delivery is null)
        {
            LogNoDeliveryData(_logger, notification.Mail.MessageId);
            return;
        }

        var email = await _dbContext.Emails
            .FirstOrDefaultAsync(e => e.SesMessageId == notification.Mail.MessageId, cancellationToken);

        if (email is null)
        {
            LogEmailNotFound(_logger, notification.Mail.MessageId);
            return;
        }

        LogDeliveryReceived(_logger, email.Id);

        // Update status to Delivered
        email.Status = EmailStatus.Delivered;

        if (DateTime.TryParse(delivery.Timestamp, out var deliveredAt))
        {
            email.DeliveredAt = deliveredAt.ToUniversalTime();
        }
        else
        {
            email.DeliveredAt = DateTime.UtcNow;
        }

        // Add delivery event
        _dbContext.EmailEvents.Add(new EmailEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            EventType = EventType.Delivered,
            Data = JsonSerializer.Serialize(new
            {
                timestamp = delivery.Timestamp,
                recipients = delivery.Recipients,
                processingTimeMillis = delivery.ProcessingTimeMillis,
                smtpResponse = delivery.SmtpResponse
            }),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish webhook dispatch message
        await _publishEndpoint.Publish(new WebhookDispatchMessage
        {
            TenantId = email.TenantId,
            EventType = "delivered",
            EmailId = email.Id,
            MessageId = email.MessageId,
            Data = JsonSerializer.Serialize(new
            {
                recipient = email.ToEmails,
                subject = email.Subject
            }),
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery notification has no delivery data for SES message {MessageId}")]
    private static partial void LogNoDeliveryData(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email not found for SES message {MessageId}")]
    private static partial void LogEmailNotFound(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivery confirmed for EmailId={EmailId}")]
    private static partial void LogDeliveryReceived(ILogger logger, Guid emailId);
}
