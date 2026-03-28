using MediatR;

namespace EaaS.Api.Features.Webhooks;

public sealed record GetWebhookQuery(
    Guid Id,
    Guid TenantId) : IRequest<WebhookDto>;
