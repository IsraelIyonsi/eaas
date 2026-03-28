using MediatR;

namespace EaaS.Api.Features.Webhooks;

public sealed record DeleteWebhookCommand(
    Guid Id,
    Guid TenantId) : IRequest;
