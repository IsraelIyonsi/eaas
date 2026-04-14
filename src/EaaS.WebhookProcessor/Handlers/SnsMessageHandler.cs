using System.Text.Json;
using EaaS.WebhookProcessor.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EaaS.WebhookProcessor.Handlers;

public sealed partial class SnsMessageHandler
{
    private readonly IBounceHandler _bounceHandler;
    private readonly IComplaintHandler _complaintHandler;
    private readonly IDeliveryHandler _deliveryHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SnsSignatureVerifier _signatureVerifier;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SnsMessageHandler> _logger;

    public SnsMessageHandler(
        IBounceHandler bounceHandler,
        IComplaintHandler complaintHandler,
        IDeliveryHandler deliveryHandler,
        IHttpClientFactory httpClientFactory,
        SnsSignatureVerifier signatureVerifier,
        IConnectionMultiplexer redis,
        ILogger<SnsMessageHandler> logger)
    {
        _bounceHandler = bounceHandler;
        _complaintHandler = complaintHandler;
        _deliveryHandler = deliveryHandler;
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
            // Malformed JSON at a signed-webhook endpoint is signature-adjacent: legit AWS SNS never sends
            // this. Treat as a probe/spoof and respond 403 (not 400) so scanners get no useful signal.
            LogDeserializationFailed(_logger, ex);
            return Results.StatusCode(403);
        }

        if (snsMessage is null)
        {
            // Empty body: same rationale as malformed JSON — SNS never sends empty bodies.
            return Results.StatusCode(403);
        }

        var requestId = request.HttpContext.TraceIdentifier;

        // Kill switch: signature check fully bypassed. Loud error log + metric every request so it
        // can't go unnoticed if left on. Ops use this only during incidents when AWS cert infra is broken.
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

        // Replay dedup: after signature passes, gate on MessageId. SNS legitimately retries on our 5xx/timeout,
        // so a duplicate MessageId is ACK'd with 200 (idempotent) — NOT 403, which would break retries.
        // Redis failure is fail-open (warn + metric + proceed): signature is already authenticated and
        // downstream bounce/complaint/delivery handlers are idempotent by MessageId.
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
            "Notification" => await HandleNotification(snsMessage, cancellationToken),
            "UnsubscribeConfirmation" => HandleUnsubscribeConfirmation(snsMessage),
            // Unknown Type survived signature verification only if an attacker controls a valid-looking
            // payload — treat as a probe/attack surface and 403, not 400.
            _ => Results.StatusCode(403)
        };
    }

    private async Task<IResult> HandleSubscriptionConfirmation(SnsMessage snsMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snsMessage.SubscribeUrl))
        {
            LogMissingSubscribeUrl(_logger);
            return Results.BadRequest(new { error = "Missing SubscribeURL" });
        }

        // Defense in depth on top of the SSRF-guarded HttpClient: refuse any SubscribeURL whose host
        // isn't anchored sns.<region>.amazonaws.com BEFORE any outbound call (C3 rev-2).
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
            LogSubscriptionConfirmed(_logger, snsMessage.TopicArn ?? "unknown");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("blocked range", StringComparison.OrdinalIgnoreCase)
                                              || ex.Message.Contains("did not resolve", StringComparison.OrdinalIgnoreCase))
        {
            // SSRF guard refused connect — bad URL, surface 400 (SNS retries can't fix a blocked IP).
            LogSubscriptionConfirmationFailed(_logger, ex);
            return Results.BadRequest(new { error = "SubscribeURL refused by SSRF guard" });
        }
        catch (Exception ex)
        {
            // Real network/upstream failure — 502 (not 500) so SNS retries. Aligned with SnsInboundHandler.
            LogSubscriptionConfirmationFailed(_logger, ex);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize SNS message")]
    private static partial void LogDeserializationFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS notification rejected (signature verification failed). RequestId={RequestId}")]
    private static partial void LogSignatureRejected(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "SNS_SIGNATURE_VERIFICATION_DISABLED — kill switch is on, accepting unverified payload. RequestId={RequestId}")]
    private static partial void LogSignatureVerificationDisabled(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS dedup unavailable (Redis error); failing open. RequestId={RequestId}")]
    private static partial void LogDedupUnavailable(ILogger logger, string requestId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "SNS duplicate MessageId suppressed. RequestId={RequestId} MessageId={MessageId}")]
    private static partial void LogDuplicateMessage(ILogger logger, string requestId, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Missing SubscribeURL in subscription confirmation")]
    private static partial void LogMissingSubscribeUrl(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS SubscribeURL rejected (host not in sns.<region>.amazonaws.com allowlist): {SubscribeUrl}")]
    private static partial void LogInvalidSubscribeUrl(ILogger logger, string subscribeUrl);

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
