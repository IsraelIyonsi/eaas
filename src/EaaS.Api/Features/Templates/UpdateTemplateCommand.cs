using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record UpdateTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    string? Name,
    string? SubjectTemplate,
    string? HtmlBody,
    string? TextBody) : IRequest<TemplateResult>;
