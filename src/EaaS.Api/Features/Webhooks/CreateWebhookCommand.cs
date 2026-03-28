using MediatR;

namespace EaaS.Api.Features.Webhooks;

public sealed record CreateWebhookCommand(
    Guid TenantId,
    string Url,
    string[] Events,
    string? Secret) : IRequest<WebhookCreatedDto>;
