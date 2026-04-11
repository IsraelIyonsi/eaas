namespace EaaS.Shared.Constants;

public static class MessagingConstants
{
    public const string EmailSendQueue = "eaas-emails-send";
    public const string WebhookDispatchQueue = "eaas-webhook-dispatch";
    public const string InboundEmailProcessQueue = "eaas-inbound-process";

    /// <summary>
    /// Priority queue for time-sensitive emails (OTP, password resets, transactional).
    /// Messages on this queue bypass the rate limiter and have dedicated concurrency.
    /// </summary>
    public const string EmailSendPriorityQueue = "eaas-emails-send-priority";

    /// <summary>
    /// Queue for high-throughput batch email processing (marketing, newsletters).
    /// Uses MassTransit batch consumer to reduce per-message overhead.
    /// </summary>
    public const string EmailSendBatchQueue = "eaas-emails-send-batch";
}
