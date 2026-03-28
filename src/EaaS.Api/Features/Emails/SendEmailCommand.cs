using MediatR;

namespace EaaS.Api.Features.Emails;

public sealed record SendEmailCommand(
    Guid TenantId,
    Guid ApiKeyId,
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
    Dictionary<string, string>? Metadata,
    string? IdempotencyKey) : IRequest<SendEmailResult>;

public sealed record SendEmailResult(Guid Id, string MessageId, string Status);
