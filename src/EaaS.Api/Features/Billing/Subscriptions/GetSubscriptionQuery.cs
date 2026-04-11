using MediatR;

namespace EaaS.Api.Features.Billing.Subscriptions;

public sealed record GetSubscriptionQuery(Guid TenantId) : IRequest<SubscriptionResult?>;
