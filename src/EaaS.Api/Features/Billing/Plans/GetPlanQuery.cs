using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public sealed record GetPlanQuery(Guid PlanId) : IRequest<PlanResult>;
