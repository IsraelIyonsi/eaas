using System.Security.Cryptography;
using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Emails;

public sealed class SendBatchHandler : IRequestHandler<SendBatchCommand, SendBatchResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly IPublishEndpoint _publishEndpoint;

    public SendBatchHandler(
        AppDbContext dbContext,
        ICacheService cacheService,
        IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<SendBatchResult> Handle(SendBatchCommand request, CancellationToken cancellationToken)
    {
        // Rate limit check: count all emails against rate limit
        var rateLimitKey = $"ratelimit:send:{request.ApiKeyId}";
        var emailCount = request.Emails.Count;

        // Check if we have enough budget for all emails
        for (var i = 0; i < emailCount; i++)
        {
            var isAllowed = await _cacheService.CheckRateLimitAsync(rateLimitKey, maxRequests: 100, TimeSpan.FromMinutes(1), cancellationToken);
            if (!isAllowed)
                throw new InvalidOperationException($"Rate limit exceeded. Maximum 100 sends per minute per API key. Batch of {emailCount} emails exceeds remaining budget.");
        }

        var batchId = $"batch_{GenerateShortId()}";
        var results = new List<BatchEmailResultItem>();
        var accepted = 0;
        var rejected = 0;

        // Pre-load verified domains for this tenant
        var verifiedDomains = await _dbContext.Domains
            .AsNoTracking()
            .Where(d => d.TenantId == request.TenantId && d.Status == DomainStatus.Verified && d.DeletedAt == null)
            .Select(d => d.DomainName)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < request.Emails.Count; i++)
        {
            var item = request.Emails[i];

            try
            {
                // Validate domain
                var fromDomain = item.From.Split('@').Last().ToLowerInvariant();
                if (!verifiedDomains.Contains(fromDomain))
                {
                    results.Add(new BatchEmailResultItem(i, null, "rejected", $"Domain '{fromDomain}' is not verified."));
                    rejected++;
                    continue;
                }

                // Check suppression for all recipients
                var allRecipients = new List<string>(item.To);
                if (item.Cc is not null) allRecipients.AddRange(item.Cc);
                if (item.Bcc is not null) allRecipients.AddRange(item.Bcc);

                var suppressedRecipient = await CheckSuppression(request.TenantId, allRecipients, cancellationToken);
                if (suppressedRecipient is not null)
                {
                    results.Add(new BatchEmailResultItem(i, null, "rejected", $"Recipient '{suppressedRecipient}' is on the suppression list."));
                    rejected++;
                    continue;
                }

                // Create Email entity
                var email = new Email
                {
                    Id = Guid.NewGuid(),
                    TenantId = request.TenantId,
                    ApiKeyId = request.ApiKeyId,
                    MessageId = $"eaas_{Guid.NewGuid():N}",
                    BatchId = batchId,
                    FromEmail = item.From,
                    ToEmails = JsonSerializer.Serialize(item.To),
                    CcEmails = item.Cc is not null ? JsonSerializer.Serialize(item.Cc) : "[]",
                    BccEmails = item.Bcc is not null ? JsonSerializer.Serialize(item.Bcc) : "[]",
                    Subject = item.Subject ?? string.Empty,
                    HtmlBody = item.HtmlBody,
                    TextBody = item.TextBody,
                    TemplateId = item.TemplateId,
                    Variables = item.Variables is not null ? JsonSerializer.Serialize(item.Variables) : null,
                    Tags = item.Tags?.ToArray() ?? Array.Empty<string>(),
                    Metadata = item.Metadata is not null ? JsonSerializer.Serialize(item.Metadata) : "{}",
                    Status = EmailStatus.Queued,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Emails.Add(email);

                _dbContext.EmailEvents.Add(new EmailEvent
                {
                    Id = Guid.NewGuid(),
                    EmailId = email.Id,
                    EventType = EventType.Queued,
                    Data = JsonSerializer.Serialize(new { batchId }),
                    CreatedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Publish to queue
                await _publishEndpoint.Publish(new SendEmailMessage
                {
                    EmailId = email.Id,
                    TenantId = email.TenantId,
                    From = email.FromEmail,
                    To = JsonSerializer.Serialize(item.To),
                    CcEmails = email.CcEmails,
                    BccEmails = email.BccEmails,
                    Subject = email.Subject,
                    HtmlBody = email.HtmlBody,
                    TextBody = email.TextBody,
                    TemplateId = email.TemplateId,
                    Variables = email.Variables,
                    Tags = email.Tags,
                    Metadata = email.Metadata
                }, cancellationToken);

                results.Add(new BatchEmailResultItem(i, email.MessageId, "queued", null));
                accepted++;
            }
            catch (Exception ex)
            {
                results.Add(new BatchEmailResultItem(i, null, "rejected", ex.Message));
                rejected++;
            }
        }

        return new SendBatchResult(batchId, request.Emails.Count, accepted, rejected, results);
    }

    private async Task<string?> CheckSuppression(Guid tenantId, List<string> recipients, CancellationToken cancellationToken)
    {
        foreach (var recipient in recipients)
        {
            var isSuppressed = await _cacheService.IsEmailSuppressedAsync(tenantId, recipient, cancellationToken);

            if (!isSuppressed)
            {
                var recipientLower = recipient.ToLowerInvariant();
                isSuppressed = await _dbContext.SuppressionEntries
                    .AsNoTracking()
                    .AnyAsync(s => s.TenantId == tenantId && s.EmailAddress == recipientLower, cancellationToken);
            }

            if (isSuppressed)
                return recipient;
        }

        return null;
    }

    private static string GenerateShortId()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[8];
        for (var i = 0; i < 8; i++)
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        return new string(result);
    }
}
