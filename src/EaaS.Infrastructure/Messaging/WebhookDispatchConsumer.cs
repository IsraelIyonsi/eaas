using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using EaaS.Shared.Utilities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.Messaging;

public sealed partial class WebhookDispatchConsumer : IConsumer<WebhookDispatchMessage>
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatchConsumer> _logger;

    public WebhookDispatchConsumer(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatchConsumer> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<WebhookDispatchMessage> context)
    {
        var message = context.Message;
        LogDispatchStarted(_logger, message.EventType, message.TenantId);

        // NOTE: tracked query — we update ConsecutiveFailures / Status on each dispatch
        // so we can auto-disable after repeated failures (C3 rev-2).
        var webhooks = await _dbContext.Webhooks
            .Where(w => w.TenantId == message.TenantId
                        && w.Status == EaaS.Domain.Enums.WebhookStatus.Active
                        && w.Events.Contains(message.EventType))
            .ToListAsync(context.CancellationToken);

        if (webhooks.Count == 0)
        {
            LogNoWebhooks(_logger, message.TenantId, message.EventType);
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            @event = message.EventType,
            message_id = message.MessageId,
            email_id = message.EmailId,
            timestamp = message.Timestamp,
            data = JsonSerializer.Deserialize<object>(message.Data)
        });

        var deliveryId = Guid.NewGuid().ToString();

        foreach (var webhook in webhooks)
        {
            await DispatchToWebhook(webhook, payload, deliveryId, message, context.CancellationToken);
        }
    }

    private async Task DispatchToWebhook(
        Webhook webhook,
        string payload,
        string deliveryId,
        WebhookDispatchMessage message,
        CancellationToken cancellationToken)
    {
        // H11 idempotency: short-circuit if a prior attempt for this exact
        // (webhook, email, event_type) tuple already succeeded. MassTransit retries
        // must never hit a customer endpoint twice.
        var deliveryRow = await _dbContext.WebhookDeliveries
            .FirstOrDefaultAsync(
                d => d.WebhookId == webhook.Id
                     && d.EmailId == message.EmailId
                     && d.EventType == message.EventType,
                cancellationToken);

        if (deliveryRow is { Status: WebhookDeliveryStatus.Succeeded })
        {
            LogDeliverySkippedDuplicate(_logger, webhook.Id, message.EventType, message.EmailId);
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (deliveryRow is null)
        {
            deliveryRow = new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookId = webhook.Id,
                EmailId = message.EmailId,
                EventType = message.EventType,
                Status = WebhookDeliveryStatus.Pending,
                FirstAttemptAt = nowUtc,
                LastAttemptAt = nowUtc,
                AttemptCount = 1
            };
            _dbContext.WebhookDeliveries.Add(deliveryRow);
        }
        else
        {
            deliveryRow.Status = WebhookDeliveryStatus.Pending;
            deliveryRow.LastAttemptAt = nowUtc;
            deliveryRow.AttemptCount++;
        }

        // Persist the pending row *before* the HTTP call so that replays / crashes
        // between the call and the post-write find a row on disk.
        await _dbContext.SaveChangesAsync(cancellationToken);

        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        EaaS.Shared.Utilities.WebhookSigner.ApplyHeaders(content, webhook.Secret, payload, message.EventType, deliveryId);

        int statusCode = 0;
        bool success = false;
        string? errorMessage = null;
        string? responseSnippet = null;

        try
        {
            // Defence in depth against Finding C3: re-validate persisted URL before
            // dispatch in case a row was created before the validator was added.
            if (!SsrfGuard.IsSyntacticallySafe(webhook.Url, out var ssrfReason))
            {
                errorMessage = $"Webhook URL rejected by SSRF guard: {ssrfReason}";
                LogDeliveryException(_logger, webhook.Id, new InvalidOperationException(errorMessage));
                EmailMetrics.WebhookDispatched.WithLabels("failed").Inc();
                SsrfGuard.RecordSsrfRejection("syntactic");
                deliveryRow.Status = WebhookDeliveryStatus.Failed;
                deliveryRow.ResponseStatusCode = null;
                await PersistDeliveryLog(webhook, message, 0, false, errorMessage, cancellationToken);
                return;
            }

            var client = _httpClientFactory.CreateClient("WebhookDispatch");
            client.Timeout = TimeSpan.FromSeconds(WebhookConstants.DispatchTimeoutSeconds);

            // Manually follow redirects so legitimate customer apex→www redirects succeed,
            // but each Location is re-validated by SsrfGuard (cap 3 hops).
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhook.Url) { Content = content };
            var sw = Stopwatch.StartNew();
            using var response = await SsrfGuard.SendWithSafeRedirectsAsync(client, requestMessage, cancellationToken);
            sw.Stop();
            EmailMetrics.WebhookDispatchDuration.WithLabels(message.TenantId.ToString()).Observe(sw.Elapsed.TotalSeconds);

            // Drain up to 64 KB of the body (truncated). Customer endpoints may legitimately
            // return a larger response but we only log a prefix for diagnostics (C3 rev-3).
            var body = await SsrfGuard.ReadBoundedStringAsync(
                response, truncate: true, cancellationToken: cancellationToken);
            if (!string.IsNullOrEmpty(body))
            {
                responseSnippet = body.Length > 1024 ? body[..1024] : body;
            }

            statusCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;

            if (!success)
            {
                errorMessage = $"HTTP {statusCode}";
                EmailMetrics.WebhookDispatched.WithLabels("failed").Inc();
                LogDeliveryFailed(_logger, webhook.Id, statusCode);
            }
            else
            {
                EmailMetrics.WebhookDispatched.WithLabels("success").Inc();
                LogDeliverySuccess(_logger, webhook.Id, message.EventType);
            }
        }
        catch (HttpRequestException ex) when (
            ex.Message.Contains("blocked range", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("did not resolve", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("SSRF", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("refused by SSRF", StringComparison.OrdinalIgnoreCase))
        {
            // ConnectCallback / redirect re-validation refused the destination. Record the
            // rejection metric and surface a generic error to avoid leaking resolved IPs.
            SsrfGuard.RecordSsrfRejection("connect_guard");
            errorMessage = "Destination refused by SSRF guard (private/restricted IP).";
            LogDeliveryException(_logger, webhook.Id, ex);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogDeliveryException(_logger, webhook.Id, ex);
        }

        // Update consecutive-failure counter and auto-disable at threshold.
        if (success)
        {
            webhook.ConsecutiveFailures = 0;
        }
        else
        {
            webhook.ConsecutiveFailures++;
            if (webhook.ConsecutiveFailures >= WebhookConstants.AutoDisableThreshold
                && webhook.Status == EaaS.Domain.Enums.WebhookStatus.Active)
            {
                webhook.Status = EaaS.Domain.Enums.WebhookStatus.Disabled;
                webhook.UpdatedAt = DateTime.UtcNow;
                LogWebhookAutoDisabled(_logger, webhook.Id, webhook.ConsecutiveFailures);
            }
        }

        // Finalise idempotency row — replays will now see Succeeded and skip.
        deliveryRow.Status = success ? WebhookDeliveryStatus.Succeeded : WebhookDeliveryStatus.Failed;
        deliveryRow.ResponseStatusCode = statusCode == 0 ? null : statusCode;
        deliveryRow.ResponseBodySnippet = responseSnippet;

        await PersistDeliveryLog(webhook, message, statusCode, success, errorMessage, cancellationToken);

        if (!success)
            throw new InvalidOperationException($"Webhook delivery to {webhook.Url} failed: {errorMessage}");
    }

    private async Task PersistDeliveryLog(
        Webhook webhook,
        WebhookDispatchMessage message,
        int statusCode,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        _dbContext.WebhookDeliveryLogs.Add(new WebhookDeliveryLog
        {
            Id = Guid.NewGuid(),
            WebhookId = webhook.Id,
            EmailId = message.EmailId,
            EventType = message.EventType,
            StatusCode = statusCode,
            Success = success,
            ErrorMessage = errorMessage,
            AttemptNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching webhook event {EventType} for tenant {TenantId}")]
    private static partial void LogDispatchStarted(ILogger logger, string eventType, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No active webhooks for tenant {TenantId} event {EventType}")]
    private static partial void LogNoWebhooks(ILogger logger, Guid tenantId, string eventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook {WebhookId} delivery succeeded for event {EventType}")]
    private static partial void LogDeliverySuccess(ILogger logger, Guid webhookId, string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook {WebhookId} delivery failed with status {StatusCode}")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid webhookId, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Webhook {WebhookId} delivery threw an exception")]
    private static partial void LogDeliveryException(ILogger logger, Guid webhookId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook {WebhookId} auto-disabled after {ConsecutiveFailures} consecutive delivery failures")]
    private static partial void LogWebhookAutoDisabled(ILogger logger, Guid webhookId, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook {WebhookId} dispatch for event {EventType} email {EmailId} skipped — prior delivery already succeeded (H11 idempotency)")]
    private static partial void LogDeliverySkippedDuplicate(ILogger logger, Guid webhookId, string eventType, Guid emailId);
}
