namespace EaaS.Infrastructure.Messaging.Contracts;

/// <summary>
/// Email message with explicit priority routing. Critical and high-priority messages
/// are routed to a dedicated queue with lower latency (no rate limiter, dedicated concurrency).
/// <list type="bullet">
///   <item><description>Normal (0): Marketing, newsletters, batch sends — default queue</description></item>
///   <item><description>High (1): Transactional emails — order confirmations, receipts</description></item>
///   <item><description>Critical (2): OTP codes, password resets, security alerts — must deliver within seconds</description></item>
/// </list>
/// </summary>
public sealed record PriorityEmailMessage
{
    public Guid EmailId { get; init; }
    public Guid TenantId { get; init; }
    public string From { get; init; } = string.Empty;
    public string? FromName { get; init; }
    public string To { get; init; } = string.Empty;
    public string CcEmails { get; init; } = "[]";
    public string BccEmails { get; init; } = "[]";
    public string Subject { get; init; } = string.Empty;
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public Guid? TemplateId { get; init; }
    public string? Variables { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();
    public string? Metadata { get; init; }
    public bool TrackOpens { get; init; } = true;
    public bool TrackClicks { get; init; } = true;
    public string Attachments { get; init; } = "[]";

    /// <summary>
    /// Delivery priority: 0 = Normal, 1 = High, 2 = Critical.
    /// Critical messages bypass rate limiting and are processed on a dedicated queue.
    /// </summary>
    public int Priority { get; init; }
}
