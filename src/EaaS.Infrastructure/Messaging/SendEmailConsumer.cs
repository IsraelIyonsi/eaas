using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.Messaging;

public sealed partial class SendEmailConsumer : IConsumer<SendEmailMessage>
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailDeliveryService _emailDeliveryService;
    private readonly ITemplateRenderingService _templateRenderingService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<SendEmailConsumer> _logger;

    public SendEmailConsumer(
        AppDbContext dbContext,
        IEmailDeliveryService emailDeliveryService,
        ITemplateRenderingService templateRenderingService,
        ICacheService cacheService,
        ILogger<SendEmailConsumer> logger)
    {
        _dbContext = dbContext;
        _emailDeliveryService = emailDeliveryService;
        _templateRenderingService = templateRenderingService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SendEmailMessage> context)
    {
        var message = context.Message;
        LogReceivedMessage(_logger, message.EmailId);

        // 1. Load Email entity from DB
        var email = await _dbContext.Emails
            .FirstOrDefaultAsync(e => e.Id == message.EmailId, context.CancellationToken);

        if (email is null)
        {
            LogEmailNotFound(_logger, message.EmailId);
            return;
        }

        var subject = email.Subject;
        var htmlBody = email.HtmlBody;
        var textBody = email.TextBody;

        try
        {
            // 2. If templateId provided: load template and render
            if (message.TemplateId.HasValue)
            {
                var template = await _dbContext.Templates
                    .AsNoTracking()
                    .Where(t => t.Id == message.TemplateId.Value && t.DeletedAt == null)
                    .FirstOrDefaultAsync(context.CancellationToken);

                if (template is null)
                {
                    await UpdateEmailStatus(email, EmailStatus.Failed, $"Template '{message.TemplateId}' not found.", context.CancellationToken);
                    return;
                }

                var variables = !string.IsNullOrWhiteSpace(message.Variables)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(message.Variables) ?? new Dictionary<string, object>()
                    : new Dictionary<string, object>();

                var rendered = await _templateRenderingService.RenderAsync(
                    template.SubjectTemplate,
                    template.HtmlBody,
                    template.TextBody,
                    variables,
                    context.CancellationToken);

                subject = rendered.Subject;
                htmlBody = rendered.HtmlBody;
                textBody = rendered.TextBody;
            }

            // 3. Update status to Sending
            await UpdateEmailStatus(email, EmailStatus.Sending, null, context.CancellationToken);

            // 4. Parse recipients
            var recipients = JsonSerializer.Deserialize<List<string>>(message.To) ?? new List<string>();

            // 5. Call SES
            var result = await _emailDeliveryService.SendEmailAsync(
                message.From,
                recipients,
                subject,
                htmlBody,
                textBody,
                context.CancellationToken);

            if (result.Success)
            {
                // SES acceptance means "sent" not "delivered" — delivery confirmation
                // comes via SNS webhooks (Sprint 2). Mark as Sending with SentAt.
                email.SesMessageId = result.MessageId;
                email.SentAt = DateTime.UtcNow;
                await UpdateEmailStatus(email, EmailStatus.Sending, null, context.CancellationToken);

                LogEmailSent(_logger, message.EmailId, result.MessageId!);
            }
            else
            {
                await UpdateEmailStatus(email, EmailStatus.Failed, result.ErrorMessage, context.CancellationToken);
                LogEmailFailed(_logger, message.EmailId, result.ErrorMessage ?? "Unknown error");

                // Throw to trigger MassTransit retry
                throw new InvalidOperationException($"SES delivery failed: {result.ErrorMessage}");
            }
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw for MassTransit retry
        }
        catch (Exception ex)
        {
            await UpdateEmailStatus(email, EmailStatus.Failed, ex.Message, context.CancellationToken);
            LogEmailException(_logger, ex, message.EmailId);
            throw; // Re-throw for MassTransit retry
        }
    }

    private async Task UpdateEmailStatus(Email email, EmailStatus status, string? errorMessage, CancellationToken cancellationToken)
    {
        email.Status = status;
        if (errorMessage is not null)
            email.ErrorMessage = errorMessage;

        var eventType = status switch
        {
            EmailStatus.Sending => EventType.Sent,
            EmailStatus.Delivered => EventType.Delivered,
            EmailStatus.Failed => EventType.Failed,
            _ => EventType.Queued
        };

        _dbContext.EmailEvents.Add(new EmailEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            EventType = eventType,
            Data = errorMessage is not null
                ? JsonSerializer.Serialize(new { error = errorMessage })
                : "{}",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Received SendEmailMessage for EmailId={EmailId}")]
    private static partial void LogReceivedMessage(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email entity not found for EmailId={EmailId}")]
    private static partial void LogEmailNotFound(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent to SES for EmailId={EmailId}, SesMessageId={SesMessageId}")]
    private static partial void LogEmailSent(ILogger logger, Guid emailId, string sesMessageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Email delivery failed for EmailId={EmailId}: {Error}")]
    private static partial void LogEmailFailed(ILogger logger, Guid emailId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error processing EmailId={EmailId}")]
    private static partial void LogEmailException(ILogger logger, Exception ex, Guid emailId);
}
