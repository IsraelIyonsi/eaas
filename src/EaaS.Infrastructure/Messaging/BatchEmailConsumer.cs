using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Providers;
using EaaS.Infrastructure.Messaging.Contracts;
using SendEmailRequest = EaaS.Domain.Providers.SendEmailRequest;
using EaaS.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.Messaging;

/// <summary>
/// Batch consumer for high-throughput email sending (marketing, newsletters, bulk sends).
/// Processes up to 50 messages per batch to amortize DB round-trip overhead:
/// - Single DB query to load all Email entities instead of N individual queries
/// - Batched status updates with a single SaveChangesAsync call
/// - Reduces connection churn on both PostgreSQL and the provider
/// </summary>
public sealed partial class BatchEmailConsumer : IConsumer<Batch<SendEmailMessage>>
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailProviderFactory _providerFactory;
    private readonly ILogger<BatchEmailConsumer> _logger;

    public BatchEmailConsumer(
        AppDbContext dbContext,
        IEmailProviderFactory providerFactory,
        ILogger<BatchEmailConsumer> logger)
    {
        _dbContext = dbContext;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Batch<SendEmailMessage>> context)
    {
        var batch = context.Message;
        LogBatchReceived(_logger, batch.Length);

        // 1. Load all Email entities in a single query to avoid N+1
        var emailIds = batch.Select(m => m.Message.EmailId).ToList();
        var emails = await _dbContext.Emails
            .Where(e => emailIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, context.CancellationToken);

        var successCount = 0;
        var failCount = 0;

        // 2. Process each message in the batch
        foreach (var item in batch)
        {
            var message = item.Message;

            if (!emails.TryGetValue(message.EmailId, out var email))
            {
                LogEmailNotFound(_logger, message.EmailId);
                failCount++;
                continue;
            }

            try
            {
                email.Status = EmailStatus.Sending;

                var recipients = JsonSerializer.Deserialize<List<string>>(message.To) ?? new List<string>();
                var ccRecipients = !string.IsNullOrWhiteSpace(message.CcEmails) && message.CcEmails != "[]"
                    ? JsonSerializer.Deserialize<List<string>>(message.CcEmails)
                    : null;
                var bccRecipients = !string.IsNullOrWhiteSpace(message.BccEmails) && message.BccEmails != "[]"
                    ? JsonSerializer.Deserialize<List<string>>(message.BccEmails)
                    : null;

                var provider = _providerFactory.GetForTenant(message.TenantId);

                var outcome = await provider.SendAsync(
                    new SendEmailRequest(
                        TenantId: message.TenantId,
                        From: message.From,
                        FromName: message.FromName,
                        To: recipients,
                        Cc: ccRecipients,
                        Bcc: bccRecipients,
                        Subject: email.Subject,
                        HtmlBody: email.HtmlBody,
                        TextBody: email.TextBody),
                    context.CancellationToken);

                if (outcome.Success)
                {
                    // Dual-write during Phase 0 — SesMessageId preserved until Phase 3 drop.
                    email.SesMessageId = outcome.ProviderMessageId;
                    email.ProviderMessageId = outcome.ProviderMessageId;
                    email.ProviderKey = provider.ProviderKey;
                    email.SentAt = DateTime.UtcNow;
                    email.Status = EmailStatus.Sending;
                    successCount++;

                    _dbContext.EmailEvents.Add(new EmailEvent
                    {
                        Id = Guid.NewGuid(),
                        EmailId = email.Id,
                        EventType = EventType.Sent,
                        Data = "{}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    email.Status = EmailStatus.Failed;
                    email.ErrorMessage = outcome.ErrorMessage;
                    failCount++;

                    _dbContext.EmailEvents.Add(new EmailEvent
                    {
                        Id = Guid.NewGuid(),
                        EmailId = email.Id,
                        EventType = EventType.Failed,
                        Data = JsonSerializer.Serialize(new { error = outcome.ErrorMessage }),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                email.Status = EmailStatus.Failed;
                email.ErrorMessage = ex.Message;
                failCount++;

                _dbContext.EmailEvents.Add(new EmailEvent
                {
                    Id = Guid.NewGuid(),
                    EmailId = email.Id,
                    EventType = EventType.Failed,
                    Data = JsonSerializer.Serialize(new { error = ex.Message }),
                    CreatedAt = DateTime.UtcNow
                });

                LogEmailFailed(_logger, message.EmailId, ex);
            }
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        if (failCount > 0 && failCount == batch.Length)
        {
            throw new InvalidOperationException(
                $"All {failCount} emails in batch failed. Triggering MassTransit retry.");
        }

        LogBatchCompleted(_logger, batch.Length, successCount, failCount);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Batch received with {Count} email messages")]
    private static partial void LogBatchReceived(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Email entity not found for EmailId={EmailId} in batch")]
    private static partial void LogEmailNotFound(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Email send failed in batch for EmailId={EmailId}")]
    private static partial void LogEmailFailed(ILogger logger, Guid emailId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Batch completed: Total={Total}, Success={Success}, Failed={Failed}")]
    private static partial void LogBatchCompleted(ILogger logger, int total, int success, int failed);
}
