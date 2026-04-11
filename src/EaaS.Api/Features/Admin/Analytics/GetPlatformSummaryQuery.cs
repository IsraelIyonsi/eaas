using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed record GetPlatformSummaryQuery : IRequest<PlatformSummaryResult>;
