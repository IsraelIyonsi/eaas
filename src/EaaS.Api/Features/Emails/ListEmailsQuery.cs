using MediatR;

namespace EaaS.Api.Features.Emails;

public sealed record ListEmailsQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    string? Status,
    DateTime? From,
    DateTime? To,
    string? Tag,
    string? FromEmail,
    string? ToEmail,
    string? Tags,
    Guid? TemplateId,
    string? BatchId,
    string? SortBy,
    string? SortDir) : IRequest<ListEmailsResult>;

public sealed record ListEmailsResult(
    IReadOnlyList<EmailSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record EmailSummaryDto(
    Guid Id,
    string MessageId,
    string From,
    List<string> To,
    string Subject,
    string Status,
    string[] Tags,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeliveredAt);
