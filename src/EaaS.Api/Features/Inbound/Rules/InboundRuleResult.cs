namespace EaaS.Api.Features.Inbound.Rules;

public sealed record InboundRuleResult(
    Guid Id,
    string Name,
    Guid DomainId,
    string DomainName,
    string MatchPattern,
    string Action,
    string? WebhookUrl,
    string? ForwardTo,
    bool IsActive,
    int Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt);
