namespace EaaS.Domain.Interfaces;

/// <summary>
/// Result of creating a subscription with an external payment provider.
/// </summary>
public sealed record CreateSubscriptionResult(
    string ExternalSubscriptionId,
    string Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    string? PaymentUrl = null);
