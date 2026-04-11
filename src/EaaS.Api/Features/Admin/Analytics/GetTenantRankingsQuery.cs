using MediatR;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed record GetTenantRankingsQuery : IRequest<IReadOnlyList<TenantRankingResult>>;
