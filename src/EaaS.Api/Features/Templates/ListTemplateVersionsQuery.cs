using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record ListTemplateVersionsQuery(
    Guid TenantId,
    Guid TemplateId,
    int Page,
    int PageSize) : IRequest<ListTemplateVersionsResult>;

public sealed record ListTemplateVersionsResult(
    IReadOnlyList<TemplateVersionResult> Items,
    int Page,
    int PageSize,
    int TotalCount);
