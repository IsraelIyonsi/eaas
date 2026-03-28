using MediatR;

namespace EaaS.Api.Features.Webhooks;

public sealed record UpdateWebhookCommand(
    Guid Id,
    Guid TenantId,
    string? Url,
    string[]? Events,
    string? Status) : IRequest<WebhookDto>;
