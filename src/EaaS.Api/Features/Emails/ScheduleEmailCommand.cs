using MediatR;

namespace EaaS.Api.Features.Emails;

public sealed record ScheduleEmailCommand(
    Guid TenantId,
    Guid ApiKeyId,
    string From,
    string To,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    Guid? TemplateId,
    Dictionary<string, string>? Variables,
    DateTime ScheduledAt) : IRequest<ScheduleEmailResult>;
