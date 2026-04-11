namespace EaaS.Infrastructure.Configuration;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "eaas";

    /// <summary>
    /// Legacy global prefetch count. Prefer per-queue settings below for fine-grained control.
    /// </summary>
    public int PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Number of messages RabbitMQ pushes to the email-send consumer before requiring ACK.
    /// Higher values improve throughput at the cost of memory. 50 balances SES burst capacity
    /// with consumer memory pressure for 140+ emails/second.
    /// </summary>
    public int EmailSendPrefetchCount { get; set; } = 50;

    /// <summary>
    /// Maximum concurrent email-send messages processed in parallel.
    /// Kept below prefetch to allow the broker to pre-buffer the next batch while current
    /// messages are in-flight. 25 threads avoids overwhelming SES API connections.
    /// </summary>
    public int EmailSendConcurrency { get; set; } = 25;

    /// <summary>
    /// Prefetch count for webhook dispatch. Lower than email-send because webhook HTTP calls
    /// have higher latency variance and we don't want to hoard messages during slow endpoints.
    /// </summary>
    public int WebhookPrefetchCount { get; set; } = 30;

    /// <summary>
    /// Maximum concurrent webhook dispatches. Limited to avoid exhausting outbound HTTP
    /// connections and to respect downstream rate limits on customer webhook endpoints.
    /// </summary>
    public int WebhookConcurrency { get; set; } = 15;

    /// <summary>
    /// Prefetch count for inbound email processing. Conservative because each message
    /// involves S3 fetches, MIME parsing, and DB writes — all memory-intensive operations.
    /// </summary>
    public int InboundPrefetchCount { get; set; } = 20;

    /// <summary>
    /// Maximum concurrent inbound email processing. Low to prevent memory spikes from
    /// large MIME attachments being parsed simultaneously.
    /// </summary>
    public int InboundConcurrency { get; set; } = 10;

    /// <summary>
    /// Maximum emails sent per second. Aligned with AWS SES account-level burst rate.
    /// Increase this as SES sending limits are raised via AWS support.
    /// </summary>
    public int EmailSendRateLimit { get; set; } = 100;

    /// <summary>
    /// Maximum webhook dispatches per second. Prevents thundering herd on customer
    /// endpoints and avoids being rate-limited or blocked by downstream services.
    /// </summary>
    public int WebhookRateLimit { get; set; } = 50;
}
