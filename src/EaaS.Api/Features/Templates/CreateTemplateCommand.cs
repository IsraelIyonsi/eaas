using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record CreateTemplateCommand(
    Guid TenantId,
    string Name,
    string SubjectTemplate,
    string HtmlTemplate,
    string? TextTemplate) : IRequest<TemplateResult>;

public sealed record TemplateResult(
    Guid Id,
    string Name,
    string SubjectTemplate,
    string HtmlTemplate,
    string? TextTemplate,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);
