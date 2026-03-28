namespace EaaS.Infrastructure.Messaging.Contracts;

public sealed record WebhookDispatchMessage
{
    public Guid TenantId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public Guid EmailId { get; init; }
    public string MessageId { get; init; } = string.Empty;
    public string Data { get; init; } = "{}";
    public DateTime Timestamp { get; init; }
}
