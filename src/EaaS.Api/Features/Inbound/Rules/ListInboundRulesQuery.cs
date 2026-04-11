using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed record ListInboundRulesQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    Guid? DomainId) : IRequest<PagedResponse<InboundRuleResult>>;
