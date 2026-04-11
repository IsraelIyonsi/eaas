namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed record SubscriptionResult(
    Guid Id,
    Guid PlanId,
    string PlanName,
    string PlanTier,
    string Status,
    string Provider,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? TrialEndsAt,
    DateTime? CancelledAt);
