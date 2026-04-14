using MediatR;

namespace EaaS.Api.Features.Emails;

/// <summary>
/// Fetches a single email for a tenant. The <paramref name="Identifier"/> can be either
/// the internal GUID (as returned by the repository) or the public <c>snx_</c>-prefixed
/// MessageId (as returned by <c>POST /emails</c>). The handler dispatches on the prefix
/// so callers can round-trip the id they got back from send without conversion (BUG-M3).
/// </summary>
public sealed record GetEmailQuery(Guid TenantId, string Identifier) : IRequest<EmailDetailResult>;

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
