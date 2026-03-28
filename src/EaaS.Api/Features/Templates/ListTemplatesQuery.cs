using MediatR;

namespace EaaS.Api.Features.Templates;

public sealed record ListTemplatesQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    string? Search = null) : IRequest<ListTemplatesResult>;

public sealed record ListTemplatesResult(
    IReadOnlyList<TemplateSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TemplateSummaryDto(
    Guid Id,
    string Name,
    string SubjectTemplate,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);
