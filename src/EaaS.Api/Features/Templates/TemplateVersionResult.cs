namespace EaaS.Api.Features.Templates;

public sealed record TemplateVersionResult(
    Guid Id,
    int Version,
    string Name,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    string? Description,
    DateTime CreatedAt);
