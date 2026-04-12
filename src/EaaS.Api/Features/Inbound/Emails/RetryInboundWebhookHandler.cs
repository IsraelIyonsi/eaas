using System.Text.Json;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Inbound.Emails;

public sealed partial class RetryInboundWebhookHandler
{
    private readonly AppDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RetryInboundWebhookHandler> _logger;

    public RetryInboundWebhookHandler(
        AppDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<RetryInboundWebhookHandler> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task HandleAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        var email = await _dbContext.InboundEmails
            .AsNoTracking()
            .Include(e => e.Attachments)
            .Where(e => e.Id == id && e.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Inbound email with id '{id}' not found.");

        await _publishEndpoint.Publish(new WebhookDispatchMessage
        {
            TenantId = tenantId,
            EventType = "email.received",
            EmailId = email.Id,
            MessageId = email.MessageId,
            Timestamp = DateTime.UtcNow,
            Data = JsonSerializer.Serialize(new
            {
                @event = "email.received",
                data = new
                {
                    id = email.Id,
                    messageId = email.MessageId,
                    from = new { email = email.FromEmail, name = email.FromName },
                    subject = email.Subject,
                    attachments = email.Attachments.Select(a => new
                    {
                        id = a.Id,
                        filename = a.Filename,
                        contentType = a.ContentType,
                        sizeBytes = a.SizeBytes
                    }),
                    verdicts = new
                    {
                        spam = email.SpamVerdict,
                        virus = email.VirusVerdict,
                        spf = email.SpfVerdict,
                        dkim = email.DkimVerdict,
                        dmarc = email.DmarcVerdict
                    },
                    receivedAt = email.ReceivedAt,
                    inReplyTo = email.InReplyTo,
                    outboundEmailId = email.OutboundEmailId
                },
                timestamp = DateTime.UtcNow
            })
        }, cancellationToken);

        LogWebhookRetried(_logger, id, tenantId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook retry dispatched: EmailId={EmailId}, TenantId={TenantId}")]
    private static partial void LogWebhookRetried(ILogger logger, Guid emailId, Guid tenantId);
}
