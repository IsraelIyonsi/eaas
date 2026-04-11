using EaaS.Domain.Exceptions;
using System.Text.Json;
using EaaS.Api.Services;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Utilities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Emails;

public sealed partial class SendEmailHandler : IRequestHandler<SendEmailCommand, SendEmailResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IRateLimiter _rateLimiter;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly SuppressionChecker _suppressionChecker;
    private readonly ISubscriptionLimitService _subscriptionLimitService;
    private readonly ILogger<SendEmailHandler> _logger;

    public SendEmailHandler(
        AppDbContext dbContext,
        IRateLimiter rateLimiter,
        IIdempotencyStore idempotencyStore,
        IPublishEndpoint publishEndpoint,
        SuppressionChecker suppressionChecker,
        ISubscriptionLimitService subscriptionLimitService,
        ILogger<SendEmailHandler> logger)
    {
        _dbContext = dbContext;
        _rateLimiter = rateLimiter;
        _idempotencyStore = idempotencyStore;
        _publishEndpoint = publishEndpoint;
        _suppressionChecker = suppressionChecker;
        _subscriptionLimitService = subscriptionLimitService;
        _logger = logger;
    }

    public async Task<SendEmailResult> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        // 0a. Check subscription quota
        var canSend = await _subscriptionLimitService.CanSendEmailAsync(request.TenantId, cancellationToken);
        if (!canSend)
            throw new QuotaExceededException("Monthly email limit exceeded. Upgrade your plan.");

        // 0b. Check rate limit per API key
        var rateLimitKey = $"ratelimit:send:{request.ApiKeyId}";
        var isAllowed = await _rateLimiter.CheckRateLimitAsync(rateLimitKey, RateLimitConstants.DefaultMaxRequestsPerMinute, RateLimitConstants.DefaultWindow, cancellationToken);
        if (!isAllowed)
            throw new RateLimitExceededException($"Rate limit exceeded. Maximum {RateLimitConstants.DefaultMaxRequestsPerMinute} sends per minute per API key.");

        // 1. Check idempotency key
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var idempotencyValue = await _idempotencyStore.GetIdempotencyKeyAsync(
                request.TenantId, request.IdempotencyKey, cancellationToken);

            if (idempotencyValue is not null)
            {
                var cached = JsonSerializer.Deserialize<IdempotencyData>(idempotencyValue);
                if (cached is not null)
                    return new SendEmailResult(cached.Id, cached.MessageId, "queued");
            }
        }

        // 2. Verify from address belongs to a verified domain for this tenant
        var fromDomain = request.From.Split('@').Last().ToLowerInvariant();
        var domainVerified = await _dbContext.Domains
            .AsNoTracking()
            .AnyAsync(d => d.TenantId == request.TenantId
                           && d.DomainName == fromDomain
                           && d.Status == DomainStatus.Verified
                           && d.DeletedAt == null, cancellationToken);

        if (!domainVerified)
            throw new DomainNotVerifiedException($"Domain '{fromDomain}' is not verified for this tenant.");

        // 3. Check all recipients against suppression list (To + CC + BCC)
        var allRecipients = new List<string>(request.To);
        if (request.Cc is not null) allRecipients.AddRange(request.Cc);
        if (request.Bcc is not null) allRecipients.AddRange(request.Bcc);

        await _suppressionChecker.EnsureNoneSuppressedOrThrowAsync(
            request.TenantId, allRecipients, cancellationToken);

        // 4. Create Email entity
        var email = new Email
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ApiKeyId = request.ApiKeyId,
            MessageId = IdGenerator.GenerateMessageId(),
            FromEmail = request.From,
            ToEmails = JsonSerializer.Serialize(request.To),
            CcEmails = request.Cc is not null ? JsonSerializer.Serialize(request.Cc) : "[]",
            BccEmails = request.Bcc is not null ? JsonSerializer.Serialize(request.Bcc) : "[]",
            Subject = request.Subject ?? string.Empty,
            HtmlBody = request.HtmlBody,
            TextBody = request.TextBody,
            TemplateId = request.TemplateId,
            Variables = request.Variables is not null ? JsonSerializer.Serialize(request.Variables) : null,
            Tags = request.Tags?.ToArray() ?? Array.Empty<string>(),
            Metadata = request.Metadata is not null ? JsonSerializer.Serialize(request.Metadata) : "{}",
            Status = EmailStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Emails.Add(email);

        // Add queued event
        _dbContext.EmailEvents.Add(new EmailEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            EventType = EventType.Queued,
            Data = "{}",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogEmailQueued(_logger, email.Id, email.MessageId, request.TenantId);

        // 5. Publish to MassTransit queue
        await _publishEndpoint.Publish(new SendEmailMessage
        {
            EmailId = email.Id,
            TenantId = email.TenantId,
            From = email.FromEmail,
            To = JsonSerializer.Serialize(request.To),
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

        // 6. Store idempotency key
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var data = JsonSerializer.Serialize(new IdempotencyData(email.Id, email.MessageId));
            await _idempotencyStore.SetIdempotencyKeyAsync(
                request.TenantId, request.IdempotencyKey, data, cancellationToken);
        }

        return new SendEmailResult(email.Id, email.MessageId, "queued");
    }

    private sealed record IdempotencyData(Guid Id, string MessageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email queued: EmailId={EmailId}, MessageId={MessageId}, TenantId={TenantId}")]
    private static partial void LogEmailQueued(ILogger logger, Guid emailId, string messageId, Guid tenantId);
}
