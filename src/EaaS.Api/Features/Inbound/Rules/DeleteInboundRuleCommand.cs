using MediatR;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed record DeleteInboundRuleCommand(
    Guid TenantId,
    Guid RuleId) : IRequest;
