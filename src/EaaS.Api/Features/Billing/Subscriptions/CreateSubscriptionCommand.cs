using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed record CreateSubscriptionCommand(
    Guid TenantId,
    Guid PlanId,
    string? Provider) : IRequest<SubscriptionResult>;
