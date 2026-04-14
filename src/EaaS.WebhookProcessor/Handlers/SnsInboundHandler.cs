using System.Text.Json;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.WebhookProcessor.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EaaS.WebhookProcessor.Handlers;

public sealed partial class SnsInboundHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SnsSignatureVerifier _signatureVerifier;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SnsInboundHandler> _logger;

    public SnsInboundHandler(
        IPublishEndpoint publishEndpoint,
        IHttpClientFactory httpClientFactory,
        SnsSignatureVerifier signatureVerifier,
        IConnectionMultiplexer redis,
        ILogger<SnsInboundHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _httpClientFactory = httpClientFactory;
        _signatureVerifier = signatureVerifier;
        _redis = redis;
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
            // Malformed JSON at a signed endpoint: return 403, not 400 — SNS never sends this, so it's
            // a probe/spoof and we give scanners no signal.
            LogDeserializationFailed(_logger, ex);
            return Results.StatusCode(403);
        }

        if (snsMessage is null)
            return Results.StatusCode(403);

        var requestId = request.HttpContext.TraceIdentifier;

        // Verify signature before any processing so spoofed inbound events are rejected. Kill switch
        // bypass logs a loud error and increments a dedicated counter every request.
        if (!_signatureVerifier.SignatureVerificationEnabled)
        {
            SnsMetrics.SignatureVerificationDisabled.Add(1);
            SnsMetrics.RecordVerificationDisabled();
            LogSignatureVerificationDisabled(_logger, requestId);
        }
        else if (!await _signatureVerifier.VerifyAsync(snsMessage, requestId, cancellationToken))
        {
            LogSignatureRejected(_logger, requestId);
            return Results.StatusCode(403);
        }

        // MessageId replay dedup — 200 idempotent on dupes, never 403 (SNS retries are legitimate).
        // Fail-open on Redis errors: signature is already authenticated and downstream is idempotent.
        if (!string.IsNullOrWhiteSpace(snsMessage.MessageId))
        {
            var key = $"sns:msgid:{snsMessage.MessageId}";
            bool firstSeen = true;
            try
            {
                var db = _redis.GetDatabase();
                firstSeen = await db.StringSetAsync(key, "1", _signatureVerifier.ReplayDedupTtl, When.NotExists);
            }
            catch (RedisException ex)
            {
                SnsMetrics.DedupUnavailable.Add(1);
                LogDedupUnavailable(_logger, requestId, ex);
            }
            catch (Exception ex)
            {
                SnsMetrics.DedupUnavailable.Add(1);
                LogDedupUnavailable(_logger, requestId, ex);
            }

            if (!firstSeen)
            {
                SnsMetrics.DedupHits.Add(1);
                LogDuplicateMessage(_logger, requestId, snsMessage.MessageId);
                return Results.Ok();
            }
        }

        return snsMessage.Type switch
        {
            "SubscriptionConfirmation" => await HandleSubscriptionConfirmation(snsMessage, cancellationToken),
            "Notification" => await HandleInboundNotification(snsMessage, cancellationToken),
            // Unknown Type past a valid signature is attacker-shaped; 403, not 400.
            _ => Results.StatusCode(403)
        };
    }

    private async Task<IResult> HandleSubscriptionConfirmation(SnsMessage snsMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snsMessage.SubscribeUrl))
            return Results.BadRequest(new { error = "Missing SubscribeURL" });

        // Defense in depth on top of the SSRF-guarded HttpClient: refuse any SubscribeURL
        // whose host isn't anchored sns.<region>.amazonaws.com (C3 rev-2).
        if (!SnsValidation.IsValidSubscribeUrl(snsMessage.SubscribeUrl))
        {
            LogInvalidSubscribeUrl(_logger, snsMessage.SubscribeUrl);
            return Results.StatusCode(403);
        }

        LogConfirmingSubscription(_logger, snsMessage.TopicArn ?? "unknown");

        try
        {
            var httpClient = _httpClientFactory.CreateClient("sns-subscribe");
            var response = await httpClient.GetAsync(snsMessage.SubscribeUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("blocked range", StringComparison.OrdinalIgnoreCase)
                                              || ex.Message.Contains("did not resolve", StringComparison.OrdinalIgnoreCase))
        {
            // ConnectCallback refused the destination (RFC1918 / IMDS / loopback / DNS-rebind). This is
            // a bad URL — 400, not 502: SNS retries won't help and we don't want to mask an attack.
            LogSubscriptionConfirmationFailed(_logger, ex);
            return Results.BadRequest(new { error = "SubscribeURL refused by SSRF guard" });
        }
        catch (Exception ex)
        {
            // Genuine upstream/network failure (timeout, 5xx, TLS). 502 so SNS retries.
            LogSubscriptionConfirmationFailed(_logger, ex);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS inbound rejected (signature verification failed). RequestId={RequestId}")]
    private static partial void LogSignatureRejected(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "SNS_SIGNATURE_VERIFICATION_DISABLED — kill switch is on, accepting unverified payload. RequestId={RequestId}")]
    private static partial void LogSignatureVerificationDisabled(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS inbound dedup unavailable (Redis error); failing open. RequestId={RequestId}")]
    private static partial void LogDedupUnavailable(ILogger logger, string requestId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "SNS inbound duplicate MessageId suppressed. RequestId={RequestId} MessageId={MessageId}")]
    private static partial void LogDuplicateMessage(ILogger logger, string requestId, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS SubscribeURL rejected (host not in sns.<region>.amazonaws.com allowlist): {SubscribeUrl}")]
    private static partial void LogInvalidSubscribeUrl(ILogger logger, string subscribeUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirming SNS inbound subscription for topic {TopicArn}")]
    private static partial void LogConfirmingSubscription(ILogger logger, string topicArn);

    [LoggerMessage(Level = LogLevel.Information, Message = "SNS inbound subscription confirmed for topic {TopicArn}")]
    private static partial void LogSubscriptionConfirmed(ILogger logger, string topicArn);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to confirm SNS inbound subscription")]
    private static partial void LogSubscriptionConfirmationFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse SES inbound notification")]
    private static partial void LogNotificationParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unexpected inbound notification type: {NotificationType}")]
    private static partial void LogUnexpectedNotificationType(ILogger logger, string notificationType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inbound email received: SesMessageId={SesMessageId}, Recipients={Recipients}")]
    private static partial void LogInboundReceived(ILogger logger, string sesMessageId, string recipients);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published inbound email to processing queue: SesMessageId={SesMessageId}")]
    private static partial void LogPublishedToQueue(ILogger logger, string sesMessageId);
}
