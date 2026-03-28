namespace EaaS.Api.Features.Webhooks;

public sealed record WebhookDto(
    Guid Id,
    string Url,
    string[] Events,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record WebhookCreatedDto(
    Guid Id,
    string Url,
    string[] Events,
    string Secret,
    string Status,
    DateTime CreatedAt);
