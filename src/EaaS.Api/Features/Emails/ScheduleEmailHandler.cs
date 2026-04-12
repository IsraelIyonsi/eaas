using System.Text.Json;
using EaaS.Api.Services;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Api.Features.Emails;

public sealed partial class ScheduleEmailHandler : IRequestHandler<ScheduleEmailCommand, ScheduleEmailResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ISubscriptionLimitService _subscriptionLimitService;
    private readonly SuppressionChecker _suppressionChecker;
    private readonly ILogger<ScheduleEmailHandler> _logger;

    public ScheduleEmailHandler(
        AppDbContext dbContext,
        ISubscriptionLimitService subscriptionLimitService,
        SuppressionChecker suppressionChecker,
        ILogger<ScheduleEmailHandler> logger)
    {
        _dbContext = dbContext;
        _subscriptionLimitService = subscriptionLimitService;
        _suppressionChecker = suppressionChecker;
        _logger = logger;
    }

    public async Task<ScheduleEmailResult> Handle(ScheduleEmailCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate schedule time
        if (request.ScheduledAt <= DateTime.UtcNow)
            throw new ValidationException("Scheduled time must not be in the past.");

        if (request.ScheduledAt > DateTime.UtcNow.AddDays(30))
            throw new ValidationException("Scheduled time must be within 30 days from now.");

        // 2. Check subscription quota
        var canSend = await _subscriptionLimitService.CanSendEmailAsync(request.TenantId, cancellationToken);
        if (!canSend)
            throw new QuotaExceededException("Monthly email limit exceeded. Upgrade your plan.");

        // 2a. Verify from address belongs to a verified domain for this tenant
        var fromDomain = request.From.Split('@').Last().ToLowerInvariant();
        var domainVerified = await _dbContext.Domains
            .AsNoTracking()
            .AnyAsync(d => d.TenantId == request.TenantId
                           && d.DomainName == fromDomain
                           && d.Status == DomainStatus.Verified
                           && d.DeletedAt == null, cancellationToken);

        if (!domainVerified)
            throw new DomainNotVerifiedException($"Domain '{fromDomain}' is not verified for this tenant.");

        // 2b. Check recipient against suppression list
        await _suppressionChecker.EnsureNoneSuppressedOrThrowAsync(
            request.TenantId, new[] { request.To }, cancellationToken);

        // 3. Create Email entity with Scheduled status
        var email = new Email
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ApiKeyId = request.ApiKeyId,
            MessageId = IdGenerator.GenerateMessageId(),
            FromEmail = request.From,
            ToEmails = JsonSerializer.Serialize(new[] { request.To }),
            Subject = request.Subject,
            HtmlBody = request.HtmlBody,
            TextBody = request.TextBody,
            TemplateId = request.TemplateId,
            Variables = request.Variables is not null ? JsonSerializer.Serialize(request.Variables) : null,
            Status = EmailStatus.Scheduled,
            ScheduledAt = request.ScheduledAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Emails.Add(email);

        // Add scheduled event
        _dbContext.EmailEvents.Add(new EmailEvent
        {
            Id = Guid.NewGuid(),
            EmailId = email.Id,
            EventType = EventType.Scheduled,
            Data = JsonSerializer.Serialize(new { scheduledAt = request.ScheduledAt }),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogEmailScheduled(_logger, email.Id, email.MessageId, request.TenantId, request.ScheduledAt);

        return new ScheduleEmailResult(email.Id, request.ScheduledAt, "scheduled");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email scheduled: EmailId={EmailId}, MessageId={MessageId}, TenantId={TenantId}, ScheduledAt={ScheduledAt}")]
    private static partial void LogEmailScheduled(ILogger logger, Guid emailId, string messageId, Guid tenantId, DateTime scheduledAt);
}
