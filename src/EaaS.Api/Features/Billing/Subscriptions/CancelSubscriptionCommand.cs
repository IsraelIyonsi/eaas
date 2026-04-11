using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed record CancelSubscriptionCommand(
    Guid TenantId,
    bool Immediate) : IRequest<SubscriptionResult>;
