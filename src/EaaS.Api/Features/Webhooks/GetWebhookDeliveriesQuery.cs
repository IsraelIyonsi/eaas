using MediatR;

namespace EaaS.Api.Features.Webhooks;

public sealed record GetWebhookDeliveriesQuery(
    Guid WebhookId,
    Guid TenantId,
    int Page,
    int PageSize,
    bool? Success) : IRequest<WebhookDeliveriesResult>;

public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid WebhookId,
    Guid EmailId,
    string EventType,
    int StatusCode,
    bool Success,
    string? ErrorMessage,
    int AttemptNumber,
    DateTime CreatedAt);

public sealed record WebhookDeliveriesResult(
    IReadOnlyList<WebhookDeliveryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
