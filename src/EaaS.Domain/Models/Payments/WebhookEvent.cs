namespace EaaS.Domain.Interfaces;

/// <summary>
/// Provider-agnostic representation of a parsed webhook event from a payment provider.
/// </summary>
public sealed record WebhookEvent(
    string EventType,
    string ExternalId,
    string? ExternalCustomerId,
    string? ExternalSubscriptionId,
    Dictionary<string, object> Data);
