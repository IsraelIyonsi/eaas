using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record PreviewTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    Dictionary<string, object>? Variables) : IRequest<PreviewTemplateResult>;

public sealed record PreviewTemplateResult(
    string Subject,
    string HtmlTemplate,
    string? TextTemplate);
