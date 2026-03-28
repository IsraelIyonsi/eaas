namespace EaaS.Shared.Contracts;

public record PagedRequest(
    int Page = 1,
    int PageSize = 50,
    string SortBy = "created_at",
    string SortDir = "desc");
