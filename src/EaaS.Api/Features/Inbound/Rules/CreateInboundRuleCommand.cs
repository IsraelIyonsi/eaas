using EaaS.Domain.Enums;
using MediatR;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed record CreateInboundRuleCommand(
    Guid TenantId,
    string Name,
    Guid DomainId,
    string MatchPattern,
    InboundRuleAction Action,
    string? WebhookUrl,
    string? ForwardTo,
    int Priority) : IRequest<InboundRuleResult>;
