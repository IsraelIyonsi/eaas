using MediatR;

namespace EaaS.Api.Features.Emails;

public sealed record GetEmailQuery(Guid TenantId, Guid Id) : IRequest<EmailDetailResult>;

public sealed record EmailDetailResult(
    Guid Id,
    string MessageId,
    string From,
    List<string> To,
    List<string>? Cc,
    List<string>? Bcc,
    string Subject,
    string Status,
    string? HtmlBody,
    string? TextBody,
    Guid? TemplateId,
    string? TemplateName,
    string[] Tags,
    List<EmailEventDto> Events,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    DateTime? OpenedAt,
    DateTime? ClickedAt);

public sealed record EmailEventDto(
    Guid Id,
    string EventType,
    string Data,
    DateTime CreatedAt);
