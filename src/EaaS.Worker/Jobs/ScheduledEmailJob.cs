using EaaS.Domain.Enums;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EaaS.Worker.Jobs;

public sealed partial class ScheduledEmailJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledEmailJob> _logger;

    public ScheduledEmailJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledEmailJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogJobStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledEmailsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogJobError(_logger, ex);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessScheduledEmailsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var now = DateTime.UtcNow;

        var scheduledEmails = await dbContext.Emails
            .Where(e => e.Status == EmailStatus.Scheduled && e.ScheduledAt <= now)
            .OrderBy(e => e.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (scheduledEmails.Count == 0)
            return;

        LogProcessingBatch(_logger, scheduledEmails.Count);

        foreach (var email in scheduledEmails)
        {
            email.Status = EmailStatus.Queued;

            await publishEndpoint.Publish(new SendEmailMessage
            {
                EmailId = email.Id,
                TenantId = email.TenantId,
                From = email.FromEmail,
                To = email.ToEmails,
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

            LogEmailDispatched(_logger, email.Id, email.TenantId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ScheduledEmailJob started")]
    private static partial void LogJobStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing scheduled emails")]
    private static partial void LogJobError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing {Count} scheduled emails")]
    private static partial void LogProcessingBatch(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled email dispatched: EmailId={EmailId}, TenantId={TenantId}")]
    private static partial void LogEmailDispatched(ILogger logger, Guid emailId, Guid tenantId);
}
