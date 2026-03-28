using MediatR;

namespace EaaS.Api.Features.Webhooks;

public sealed record ListWebhooksQuery(
    Guid TenantId,
    int Page,
    int PageSize) : IRequest<ListWebhooksResult>;

public sealed record ListWebhooksResult(
    IReadOnlyList<WebhookDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
