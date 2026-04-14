namespace EaaS.Domain.Interfaces;

/// <summary>
/// Request payload for creating a subscription against an external payment provider.
/// </summary>
public sealed record CreateSubscriptionRequest(
    string ExternalCustomerId,
    string PlanExternalId,
    string? CouponCode = null);
