using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Analytics;

public sealed class GetAnalyticsSummaryHandler : IRequestHandler<GetAnalyticsSummaryQuery, AnalyticsSummaryResult>
{
    private readonly AppDbContext _dbContext;

    public GetAnalyticsSummaryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AnalyticsSummaryResult> Handle(GetAnalyticsSummaryQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Emails
            .AsNoTracking()
            .Where(e => e.TenantId == request.TenantId
                        && e.CreatedAt >= request.DateFrom
                        && e.CreatedAt <= request.DateTo);

        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            var domainPattern = $"%@{request.Domain}";
            query = query.Where(e => EF.Functions.ILike(e.FromEmail, domainPattern));
        }

        if (request.ApiKeyId.HasValue)
            query = query.Where(e => e.ApiKeyId == request.ApiKeyId.Value);

        if (request.TemplateId.HasValue)
            query = query.Where(e => e.TemplateId == request.TemplateId.Value);

        var counts = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalSent = g.Count(),
                Delivered = g.Count(e => e.Status == EmailStatus.Delivered),
                Bounced = g.Count(e => e.Status == EmailStatus.Bounced),
                Complained = g.Count(e => e.Status == EmailStatus.Complained),
                Failed = g.Count(e => e.Status == EmailStatus.Failed),
                Opened = g.Count(e => e.OpenedAt != null),
                Clicked = g.Count(e => e.ClickedAt != null)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (counts is null || counts.TotalSent == 0)
        {
            return new AnalyticsSummaryResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var totalSent = (double)counts.TotalSent;

        return new AnalyticsSummaryResult(
            counts.TotalSent,
            counts.Delivered,
            counts.Bounced,
            counts.Complained,
            counts.Opened,
            counts.Clicked,
            counts.Failed,
            DeliveryRate: Math.Round(counts.Delivered / totalSent * 100, 2),
            OpenRate: Math.Round(counts.Opened / totalSent * 100, 2),
            ClickRate: Math.Round(counts.Clicked / totalSent * 100, 2),
            BounceRate: Math.Round(counts.Bounced / totalSent * 100, 2),
            ComplaintRate: Math.Round(counts.Complained / totalSent * 100, 2));
    }
}
