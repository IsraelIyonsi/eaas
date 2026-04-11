using MediatR;

namespace EaaS.Api.Features.Admin.Users;

public sealed record CreateAdminUserCommand(
    Guid AdminUserId,
    string Email,
    string DisplayName,
    string Password,
    string Role) : IRequest<AdminUserResult>;
