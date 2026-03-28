using MediatR;

namespace EaaS.Api.Features.Emails;

public sealed record GetEmailQuery(Guid TenantId, string MessageId) : IRequest<EmailDetailResult>;

public sealed record EmailDetailResult(
    Guid Id,
    string MessageId,
    string From,
    List<string> To,
    string Subject,
    string Status,
    List<EmailEventDto> Events,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeliveredAt);

public sealed record EmailEventDto(
    Guid Id,
    string EventType,
    string Data,
    DateTime CreatedAt);
