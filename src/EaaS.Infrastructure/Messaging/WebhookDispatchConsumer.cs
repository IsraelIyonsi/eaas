using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
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

        var webhooks = await _dbContext.Webhooks
            .AsNoTracking()
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
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Compute HMAC-SHA256 signature
        if (!string.IsNullOrWhiteSpace(webhook.Secret))
        {
            var keyBytes = Encoding.UTF8.GetBytes(webhook.Secret);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
            var signature = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
            content.Headers.Add("X-EaaS-Signature", signature);
        }

        content.Headers.Add("X-EaaS-Event", message.EventType);
        content.Headers.Add("X-EaaS-Delivery-Id", deliveryId);

        int statusCode = 0;
        bool success = false;
        string? errorMessage = null;

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookDispatch");
            client.Timeout = TimeSpan.FromSeconds(WebhookConstants.DispatchTimeoutSeconds);

            var response = await client.PostAsync(webhook.Url, content, cancellationToken);
            statusCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;

            if (!success)
            {
                errorMessage = $"HTTP {statusCode}";
                LogDeliveryFailed(_logger, webhook.Id, statusCode);
            }
            else
            {
                LogDeliverySuccess(_logger, webhook.Id, message.EventType);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            LogDeliveryException(_logger, webhook.Id, ex);
        }

        // Log delivery result
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

        if (!success)
            throw new InvalidOperationException($"Webhook delivery to {webhook.Url} failed: {errorMessage}");
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
}
