using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.Users;

public sealed record ListAdminUsersQuery(
    int Page,
    int PageSize) : IRequest<PagedResponse<AdminUserResult>>;
