using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public sealed record ListPlansQuery() : IRequest<List<PlanResult>>;
