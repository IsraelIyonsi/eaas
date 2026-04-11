using MediatR;

namespace EaaS.Api.Features.Billing.Webhooks;

public sealed record ProcessPaymentWebhookCommand(
    string Provider,
    string Payload,
    string Signature) : IRequest<Unit>;
