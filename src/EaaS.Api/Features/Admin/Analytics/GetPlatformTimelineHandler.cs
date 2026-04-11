using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Analytics;

public sealed class GetPlatformTimelineHandler : IRequestHandler<GetPlatformTimelineQuery, IReadOnlyList<TimelineDataPoint>>
{
    private readonly AppDbContext _dbContext;

    public GetPlatformTimelineHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TimelineDataPoint>> Handle(GetPlatformTimelineQuery request, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-30);

        var data = await _dbContext.Emails
            .AsNoTracking()
            .Where(e => e.CreatedAt >= since)
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new TimelineDataPoint(
                DateOnly.FromDateTime(g.Key),
                g.Count()))
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);

        return data;
    }
}
