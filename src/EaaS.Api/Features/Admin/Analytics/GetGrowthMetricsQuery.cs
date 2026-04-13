using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed record GetGrowthMetricsQuery : IRequest<GrowthMetricResult>;
