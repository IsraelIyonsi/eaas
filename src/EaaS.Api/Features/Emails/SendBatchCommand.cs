using MediatR;

namespace EaaS.Api.Features.Emails;

public sealed record SendBatchCommand(
    Guid TenantId,
    Guid ApiKeyId,
    List<BatchEmailItem> Emails) : IRequest<SendBatchResult>;

public sealed record BatchEmailItem(
    string From,
    List<string> To,
    List<string>? Cc,
    List<string>? Bcc,
    string? Subject,
    string? HtmlBody,
    string? TextBody,
    Guid? TemplateId,
    Dictionary<string, object>? Variables,
    List<string>? Tags,
    Dictionary<string, string>? Metadata);

public sealed record SendBatchResult(
    string BatchId,
    int Total,
    int Accepted,
    int Rejected,
    List<BatchEmailResultItem> Messages);

public sealed record BatchEmailResultItem(
    int Index,
    string? MessageId,
    string Status,
    string? Error);
