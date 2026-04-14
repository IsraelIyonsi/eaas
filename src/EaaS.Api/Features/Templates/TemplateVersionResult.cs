namespace EaaS.Api.Features.Templates;

public sealed record TemplateVersionResult(
    Guid Id,
    int Version,
    string Name,
    string Subject,
    string? HtmlTemplate,
    string? TextTemplate,
    string? Description,
    DateTime CreatedAt);
