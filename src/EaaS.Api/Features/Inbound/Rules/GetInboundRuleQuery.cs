using MediatR;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed record GetInboundRuleQuery(
    Guid TenantId,
    Guid RuleId) : IRequest<InboundRuleResult>;
