using MediatR;

namespace EaaS.Api.Features.Analytics;

public sealed record GetAnalyticsSummaryQuery(
    Guid TenantId,
    DateTime DateFrom,
    DateTime DateTo,
    string? Domain,
    Guid? ApiKeyId,
    Guid? TemplateId) : IRequest<AnalyticsSummaryResult>;

public sealed record AnalyticsSummaryResult(
    int TotalSent,
    int Delivered,
    int Bounced,
    int Complained,
    int Opened,
    int Clicked,
    int Failed,
    double DeliveryRate,
    double OpenRate,
    double ClickRate,
    double BounceRate,
    double ComplaintRate);
