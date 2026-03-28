using MediatR;

namespace EaaS.Api.Features.Suppressions;

public sealed record ListSuppressionsQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    string? Reason,
    string? Search) : IRequest<ListSuppressionsResult>;

public sealed record ListSuppressionsResult(
    IReadOnlyList<SuppressionDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record SuppressionDto(
    Guid Id,
    string EmailAddress,
    string Reason,
    string? SourceMessageId,
    DateTime SuppressedAt);
