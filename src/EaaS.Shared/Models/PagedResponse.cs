namespace EaaS.Shared.Contracts;

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
