using MediatR;

namespace EaaS.Api.Features.Analytics;

public sealed record GetAnalyticsTimelineQuery(
    Guid TenantId,
    DateTime DateFrom,
    DateTime DateTo,
    string Granularity,
    string? Domain,
    Guid? ApiKeyId,
    Guid? TemplateId) : IRequest<AnalyticsTimelineResult>;

public sealed record AnalyticsTimelineResult(
    string Granularity,
    IReadOnlyList<TimelinePoint> Points);

public sealed record TimelinePoint(
    DateTime Timestamp,
    int Sent,
    int Delivered,
    int Bounced,
    int Complained,
    int Opened,
    int Clicked);
