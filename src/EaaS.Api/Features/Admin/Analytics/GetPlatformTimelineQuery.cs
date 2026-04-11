using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed record GetPlatformTimelineQuery : IRequest<IReadOnlyList<TimelineDataPoint>>;
