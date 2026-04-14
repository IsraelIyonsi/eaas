namespace EaaS.Domain.Interfaces;

/// <summary>
/// Snapshot of a subscription's current state as reported by an external payment provider.
/// </summary>
public sealed record SubscriptionInfo(
    string ExternalSubscriptionId,
    string Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    string? CancelAtPeriodEnd);
