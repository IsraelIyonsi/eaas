using EaaS.Shared.Constants;

namespace EaaS.Shared.Contracts;

public record PagedRequest(
    int Page = 1,
    int PageSize = PaginationConstants.DefaultPageSize,
    string SortBy = "created_at",
    string SortDir = "desc");
