using EaaS.Domain.Enums;
using MediatR;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed record UpdateInboundRuleCommand(
    Guid TenantId,
    Guid RuleId,
    string? Name,
    string? MatchPattern,
    InboundRuleAction? Action,
    string? WebhookUrl,
    string? ForwardTo,
    bool? IsActive,
    int? Priority) : IRequest<InboundRuleResult>;
