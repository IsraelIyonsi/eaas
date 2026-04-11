using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.Messaging;

public sealed partial class InboundEmailConsumer : IConsumer<ProcessInboundEmailMessage>
{
    private readonly AppDbContext _dbContext;
    private readonly IInboundEmailStorage _storage;
    private readonly IInboundEmailParser _parser;
    private readonly ILogger<InboundEmailConsumer> _logger;

    public InboundEmailConsumer(
        AppDbContext dbContext,
        IInboundEmailStorage storage,
        IInboundEmailParser parser,
        ILogger<InboundEmailConsumer> logger)
    {
        _dbContext = dbContext;
        _storage = storage;
        _parser = parser;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessInboundEmailMessage> context)
    {
        var message = context.Message;
        LogReceivedMessage(_logger, message.SesMessageId);

        // 1. Resolve tenant from recipient domain
        var tenantId = await ResolveTenantAsync(message.Recipients, context.CancellationToken);
        if (tenantId is null)
        {
            LogNoTenantMatch(_logger, string.Join(", ", message.Recipients));
            return;
        }

        // 2. Fetch raw MIME from S3
        using var rawStream = await _storage.GetRawEmailAsync(
            message.S3ObjectKey, context.CancellationToken);

        // 3. Parse MIME
        using var parsed = _parser.Parse(rawStream);

        // 4. Create InboundEmail entity
        var emailId = Guid.NewGuid();
        var inboundEmail = new InboundEmail
        {
            Id = emailId,
            TenantId = tenantId.Value,
            MessageId = parsed.Headers.GetValueOrDefault("Message-ID") ?? message.SesMessageId,
            FromEmail = parsed.FromEmail,
            FromName = parsed.FromName,
            ToEmails = JsonSerializer.Serialize(parsed.ToAddresses),
            CcEmails = JsonSerializer.Serialize(parsed.CcAddresses),
            BccEmails = JsonSerializer.Serialize(parsed.BccAddresses),
            ReplyTo = parsed.ReplyTo,
            Subject = parsed.Subject,
            HtmlBody = parsed.HtmlBody,
            TextBody = parsed.TextBody,
            Headers = JsonSerializer.Serialize(parsed.Headers),
            Status = InboundEmailStatus.Processing,
            SpamVerdict = message.SpamVerdict,
            VirusVerdict = message.VirusVerdict,
            SpfVerdict = message.SpfVerdict,
            DkimVerdict = message.DkimVerdict,
            DmarcVerdict = message.DmarcVerdict,
            InReplyTo = parsed.InReplyTo,
            References = parsed.References,
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // 5. Reply tracking — match In-Reply-To to outbound emails
        if (!string.IsNullOrEmpty(parsed.InReplyTo))
        {
            var outboundEmail = await _dbContext.Emails
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId.Value && e.MessageId == parsed.InReplyTo)
                .Select(e => new { e.Id })
                .FirstOrDefaultAsync(context.CancellationToken);

            if (outboundEmail is not null)
            {
                inboundEmail.OutboundEmailId = outboundEmail.Id;
                LogReplyMatched(_logger, emailId, outboundEmail.Id);
            }
        }

        // 6. Use SES S3 key directly (email already stored by SES)
        inboundEmail.S3Key = message.S3ObjectKey;

        try
        {
            // 7. Process attachments
            foreach (var attachment in parsed.Attachments)
            {
                var s3Key = await _storage.StoreAttachmentAsync(
                    tenantId.Value, emailId, attachment.Filename, attachment.Content, context.CancellationToken);

                inboundEmail.Attachments.Add(new InboundAttachment
                {
                    Id = Guid.NewGuid(),
                    InboundEmailId = emailId,
                    Filename = attachment.Filename,
                    ContentType = attachment.ContentType,
                    SizeBytes = attachment.SizeBytes,
                    S3Key = s3Key,
                    ContentId = attachment.ContentId,
                    IsInline = attachment.IsInline,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // 8. Save to database
            _dbContext.InboundEmails.Add(inboundEmail);
            inboundEmail.Status = InboundEmailStatus.Processed;
            inboundEmail.ProcessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            EmailMetrics.EmailsReceived.WithLabels(tenantId.Value.ToString(), "success").Inc();
        }
        catch (Exception ex)
        {
            EmailMetrics.EmailsReceived.WithLabels(tenantId?.ToString() ?? "unknown", "failed").Inc();
            LogProcessingFailed(_logger, emailId, ex);

            // Try to mark as failed in DB
            try
            {
                inboundEmail.Status = InboundEmailStatus.Failed;
                await _dbContext.SaveChangesAsync(context.CancellationToken);
            }
            catch
            {
                // DB save also failed — MassTransit retry will handle this
            }

            throw; // Re-throw for MassTransit retry
        }

        // 9. Match rules and dispatch webhooks
        var rules = await _dbContext.InboundRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId.Value && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(context.CancellationToken);

        foreach (var rule in rules)
        {
            if (!MatchesPattern(rule.MatchPattern, message.Recipients))
                continue;

            if (rule.Action == InboundRuleAction.Webhook && !string.IsNullOrEmpty(rule.WebhookUrl))
            {
                await context.Publish(new WebhookDispatchMessage
                {
                    TenantId = tenantId!.Value,
                    EventType = "email.received",
                    EmailId = emailId,
                    MessageId = inboundEmail.MessageId,
                    Timestamp = DateTime.UtcNow,
                    Data = JsonSerializer.Serialize(new
                    {
                        @event = "email.received",
                        data = new
                        {
                            id = emailId,
                            messageId = inboundEmail.MessageId,
                            from = new { email = parsed.FromEmail, name = parsed.FromName },
                            to = parsed.ToAddresses,
                            cc = parsed.CcAddresses,
                            subject = parsed.Subject,
                            textBody = parsed.TextBody,
                            htmlBody = parsed.HtmlBody,
                            attachments = inboundEmail.Attachments.Select(a => new
                            {
                                id = a.Id,
                                filename = a.Filename,
                                contentType = a.ContentType,
                                sizeBytes = a.SizeBytes
                            }),
                            verdicts = new
                            {
                                spam = message.SpamVerdict,
                                virus = message.VirusVerdict,
                                spf = message.SpfVerdict,
                                dkim = message.DkimVerdict,
                                dmarc = message.DmarcVerdict
                            },
                            receivedAt = inboundEmail.ReceivedAt,
                            inReplyTo = parsed.InReplyTo,
                            outboundEmailId = inboundEmail.OutboundEmailId
                        },
                        timestamp = DateTime.UtcNow
                    })
                }, context.CancellationToken);

                LogWebhookDispatched(_logger, emailId, rule.WebhookUrl);
            }

            break; // First matching rule wins
        }

        LogProcessed(_logger, emailId, tenantId.Value);
    }

    private async Task<Guid?> ResolveTenantAsync(string[] recipients, CancellationToken ct)
    {
        foreach (var recipient in recipients)
        {
            var domain = recipient.Split('@').LastOrDefault();
            if (string.IsNullOrEmpty(domain))
                continue;

            var sendingDomain = await _dbContext.Domains
                .AsNoTracking()
                .Where(d => d.DomainName == domain && d.Status == DomainStatus.Verified && d.DeletedAt == null)
                .Select(d => new { d.TenantId })
                .FirstOrDefaultAsync(ct);

            if (sendingDomain is not null)
                return sendingDomain.TenantId;
        }

        return null;
    }

    private static bool MatchesPattern(string pattern, string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            if (pattern == "*@" || pattern == "*")
                return true;

            if (pattern.EndsWith('@') && recipient.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(pattern, recipient, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Received inbound email notification: SesMessageId={SesMessageId}")]
    private static partial void LogReceivedMessage(ILogger logger, string sesMessageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No tenant matches recipients: {Recipients}")]
    private static partial void LogNoTenantMatch(ILogger logger, string recipients);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reply matched: InboundEmailId={InboundEmailId} -> OutboundEmailId={OutboundEmailId}")]
    private static partial void LogReplyMatched(ILogger logger, Guid inboundEmailId, Guid outboundEmailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook dispatched for InboundEmailId={InboundEmailId} to {WebhookUrl}")]
    private static partial void LogWebhookDispatched(ILogger logger, Guid inboundEmailId, string webhookUrl);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process inbound email: EmailId={EmailId}")]
    private static partial void LogProcessingFailed(ILogger logger, Guid emailId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inbound email processed: EmailId={EmailId}, TenantId={TenantId}")]
    private static partial void LogProcessed(ILogger logger, Guid emailId, Guid tenantId);
}
