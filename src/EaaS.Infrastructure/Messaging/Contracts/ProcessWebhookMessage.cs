namespace EaaS.Infrastructure.Messaging.Contracts;

public sealed record ProcessWebhookMessage
{
    public Guid WebhookId { get; init; }
    public Guid EmailId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
}
