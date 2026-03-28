namespace EaaS.Domain.Entities;

public class WebhookDeliveryLog
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public Guid EmailId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Webhook Webhook { get; set; } = null!;
}
