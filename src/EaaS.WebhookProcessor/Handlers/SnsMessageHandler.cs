using System.Text.Json;
using EaaS.WebhookProcessor.Models;
using Microsoft.Extensions.Logging;

namespace EaaS.WebhookProcessor.Handlers;

public sealed partial class SnsMessageHandler
{
    private readonly BounceHandler _bounceHandler;
    private readonly ComplaintHandler _complaintHandler;
    private readonly DeliveryHandler _deliveryHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SnsMessageHandler> _logger;

    public SnsMessageHandler(
        BounceHandler bounceHandler,
        ComplaintHandler complaintHandler,
        DeliveryHandler deliveryHandler,
        IHttpClientFactory httpClientFactory,
        ILogger<SnsMessageHandler> logger)
    {
        _bounceHandler = bounceHandler;
        _complaintHandler = complaintHandler;
        _deliveryHandler = deliveryHandler;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        SnsMessage? snsMessage;
        try
        {
            snsMessage = JsonSerializer.Deserialize<SnsMessage>(body);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(_logger, ex);
            return Results.BadRequest(new { error = "Invalid JSON" });
        }

        if (snsMessage is null)
            return Results.BadRequest(new { error = "Empty message" });

        // Validate that the signing cert URL is from AWS
        if (!IsValidSigningCertUrl(snsMessage.SigningCertUrl))
        {
            LogInvalidCertUrl(_logger, snsMessage.SigningCertUrl ?? "null");
            return Results.StatusCode(403);
        }

        return snsMessage.Type switch
        {
            "SubscriptionConfirmation" => await HandleSubscriptionConfirmation(snsMessage, cancellationToken),
            "Notification" => await HandleNotification(snsMessage, cancellationToken),
            "UnsubscribeConfirmation" => HandleUnsubscribeConfirmation(snsMessage),
            _ => Results.BadRequest(new { error = $"Unknown message type: {snsMessage.Type}" })
        };
    }

    private async Task<IResult> HandleSubscriptionConfirmation(SnsMessage snsMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snsMessage.SubscribeUrl))
        {
            LogMissingSubscribeUrl(_logger);
            return Results.BadRequest(new { error = "Missing SubscribeURL" });
        }

        LogConfirmingSubscription(_logger, snsMessage.TopicArn ?? "unknown");

        try
        {
            var httpClient = _httpClientFactory.CreateClient("SnsConfirmation");
            var response = await httpClient.GetAsync(snsMessage.SubscribeUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            LogSubscriptionConfirmed(_logger, snsMessage.TopicArn ?? "unknown");
        }
        catch (Exception ex)
        {
            LogSubscriptionConfirmationFailed(_logger, ex);
            return Results.StatusCode(500);
        }

        return Results.Ok();
    }

    private async Task<IResult> HandleNotification(SnsMessage snsMessage, CancellationToken cancellationToken)
    {
        SesNotification? notification;
        try
        {
            notification = JsonSerializer.Deserialize<SesNotification>(snsMessage.Message);
        }
        catch (JsonException ex)
        {
            LogNotificationParseFailed(_logger, ex);
            return Results.BadRequest(new { error = "Invalid SES notification JSON" });
        }

        if (notification is null)
            return Results.BadRequest(new { error = "Empty notification" });

        LogNotificationReceived(_logger, notification.NotificationType, notification.Mail.MessageId);

        switch (notification.NotificationType)
        {
            case "Bounce":
                await _bounceHandler.HandleAsync(notification, cancellationToken);
                break;
            case "Complaint":
                await _complaintHandler.HandleAsync(notification, cancellationToken);
                break;
            case "Delivery":
                await _deliveryHandler.HandleAsync(notification, cancellationToken);
                break;
            default:
                LogUnknownNotificationType(_logger, notification.NotificationType);
                break;
        }

        return Results.Ok();
    }

    private static IResult HandleUnsubscribeConfirmation(SnsMessage snsMessage)
    {
        // Acknowledge but do nothing — we want to remain subscribed
        return Results.Ok();
    }

    private static bool IsValidSigningCertUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == "https"
               && uri.Host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize SNS message")]
    private static partial void LogDeserializationFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid SNS signing cert URL: {Url}")]
    private static partial void LogInvalidCertUrl(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Missing SubscribeURL in subscription confirmation")]
    private static partial void LogMissingSubscribeUrl(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirming SNS subscription for topic {TopicArn}")]
    private static partial void LogConfirmingSubscription(ILogger logger, string topicArn);

    [LoggerMessage(Level = LogLevel.Information, Message = "SNS subscription confirmed for topic {TopicArn}")]
    private static partial void LogSubscriptionConfirmed(ILogger logger, string topicArn);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to confirm SNS subscription")]
    private static partial void LogSubscriptionConfirmationFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse SES notification from SNS message")]
    private static partial void LogNotificationParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received SES notification: Type={NotificationType}, MessageId={MessageId}")]
    private static partial void LogNotificationReceived(ILogger logger, string notificationType, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown SES notification type: {NotificationType}")]
    private static partial void LogUnknownNotificationType(ILogger logger, string notificationType);
}
