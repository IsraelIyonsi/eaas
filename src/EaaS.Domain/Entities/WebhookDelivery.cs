using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

/// <summary>
/// Idempotency record for webhook dispatch (Tranche 2 G5 / H11). Unique on
/// <c>(WebhookId, EmailId, EventType)</c>: the consumer short-circuits when a row
/// with <see cref="WebhookDeliveryStatus.Succeeded"/> already exists for the tuple
/// so MassTransit retries never hit a customer endpoint twice.
/// </summary>
public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public Guid EmailId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

    public DateTime FirstAttemptAt { get; set; }
    public DateTime LastAttemptAt { get; set; }
    public int AttemptCount { get; set; }

    public int? ResponseStatusCode { get; set; }

    /// <summary>Truncated (max 1 KB) prefix of the response body for diagnostics.</summary>
    public string? ResponseBodySnippet { get; set; }

    // Navigation
    public Webhook Webhook { get; set; } = null!;
}
