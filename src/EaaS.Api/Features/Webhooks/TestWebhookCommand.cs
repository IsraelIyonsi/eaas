using MediatR;

namespace EaaS.Api.Features.Webhooks;

public sealed record TestWebhookCommand(
    Guid Id,
    Guid TenantId) : IRequest<TestWebhookResult>;

public sealed record TestWebhookResult(
    bool Success,
    int StatusCode,
    string? ErrorMessage);
