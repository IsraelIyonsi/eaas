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

/// <summary>Handler abstraction so <see cref="SnsMessageHandler"/> is testable via NSubstitute.</summary>
public interface IBounceHandler
{
    Task HandleAsync(SesNotification notification, CancellationToken cancellationToken);
}

public sealed partial class BounceHandler : IBounceHandler
{
    private readonly AppDbContext _dbContext;
    private readonly RecipientSuppressor _suppressor;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BounceHandler> _logger;

    public BounceHandler(AppDbContext dbContext, RecipientSuppressor suppressor, IPublishEndpoint publishEndpoint, ILogger<BounceHandler> logger)
    {
        _dbContext = dbContext;
        _suppressor = suppressor;
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
        EmailMetrics.BouncesTotal.WithLabels(email.TenantId.ToString(), bounce.BounceType.ToLowerInvariant()).Inc();

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
                await _suppressor.SuppressAsync(
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bounce notification has no bounce data for SES message {MessageId}")]
    private static partial void LogNoBounceData(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email not found for SES message {MessageId}")]
    private static partial void LogEmailNotFound(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bounce received: Type={BounceType}, SubType={BounceSubType}, EmailId={EmailId}")]
    private static partial void LogBounceReceived(ILogger logger, string bounceType, string bounceSubType, Guid emailId);
}
