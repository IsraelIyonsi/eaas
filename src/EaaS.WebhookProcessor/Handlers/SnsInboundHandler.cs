using System.Text.Json;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.WebhookProcessor.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EaaS.WebhookProcessor.Handlers;

public sealed partial class SnsInboundHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SnsInboundHandler> _logger;

    public SnsInboundHandler(
        IPublishEndpoint publishEndpoint,
        IHttpClientFactory httpClientFactory,
        ILogger<SnsInboundHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
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

        // Validate that the signing cert URL is from AWS before processing
        if (!SnsValidation.IsValidSigningCertUrl(snsMessage.SigningCertUrl))
        {
            LogInvalidCertUrl(_logger, snsMessage.SigningCertUrl ?? "null");
            return Results.StatusCode(403);
        }

        return snsMessage.Type switch
        {
            "SubscriptionConfirmation" => await HandleSubscriptionConfirmation(snsMessage, cancellationToken),
            "Notification" => await HandleInboundNotification(snsMessage, cancellationToken),
            _ => Results.BadRequest(new { error = $"Unknown message type: {snsMessage.Type}" })
        };
    }

    private async Task<IResult> HandleSubscriptionConfirmation(SnsMessage snsMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snsMessage.SubscribeUrl))
            return Results.BadRequest(new { error = "Missing SubscribeURL" });

        LogConfirmingSubscription(_logger, snsMessage.TopicArn ?? "unknown");

        var httpClient = _httpClientFactory.CreateClient("SnsConfirmation");
        var response = await httpClient.GetAsync(snsMessage.SubscribeUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        LogSubscriptionConfirmed(_logger, snsMessage.TopicArn ?? "unknown");
        return Results.Ok();
    }

    private async Task<IResult> HandleInboundNotification(SnsMessage snsMessage, CancellationToken cancellationToken)
    {
        SesInboundNotification? notification;
        try
        {
            notification = JsonSerializer.Deserialize<SesInboundNotification>(snsMessage.Message);
        }
        catch (JsonException ex)
        {
            LogNotificationParseFailed(_logger, ex);
            return Results.BadRequest(new { error = "Invalid SES inbound notification JSON" });
        }

        if (notification is null || notification.NotificationType != "Received")
        {
            LogUnexpectedNotificationType(_logger, notification?.NotificationType ?? "null");
            return Results.Ok();
        }

        var receipt = notification.Receipt;
        var mail = notification.Mail;

        LogInboundReceived(_logger, mail.MessageId, string.Join(", ", mail.Destination));

        await _publishEndpoint.Publish(new ProcessInboundEmailMessage
        {
            S3BucketName = receipt.Action.BucketName,
            S3ObjectKey = receipt.Action.ObjectKey,
            SesMessageId = mail.MessageId,
            Recipients = mail.Destination.ToArray(),
            SpamVerdict = receipt.SpamVerdict.Status,
            VirusVerdict = receipt.VirusVerdict.Status,
            SpfVerdict = receipt.SpfVerdict.Status,
            DkimVerdict = receipt.DkimVerdict.Status,
            DmarcVerdict = receipt.DmarcVerdict.Status
        }, cancellationToken);

        LogPublishedToQueue(_logger, mail.MessageId);
        return Results.Ok();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize SNS inbound message")]
    private static partial void LogDeserializationFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid SNS inbound signing cert URL: {Url}")]
    private static partial void LogInvalidCertUrl(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirming SNS inbound subscription for topic {TopicArn}")]
    private static partial void LogConfirmingSubscription(ILogger logger, string topicArn);

    [LoggerMessage(Level = LogLevel.Information, Message = "SNS inbound subscription confirmed for topic {TopicArn}")]
    private static partial void LogSubscriptionConfirmed(ILogger logger, string topicArn);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse SES inbound notification")]
    private static partial void LogNotificationParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unexpected inbound notification type: {NotificationType}")]
    private static partial void LogUnexpectedNotificationType(ILogger logger, string notificationType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inbound email received: SesMessageId={SesMessageId}, Recipients={Recipients}")]
    private static partial void LogInboundReceived(ILogger logger, string sesMessageId, string recipients);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published inbound email to processing queue: SesMessageId={SesMessageId}")]
    private static partial void LogPublishedToQueue(ILogger logger, string sesMessageId);
}
