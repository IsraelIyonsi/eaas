using MediatR;

namespace EaaS.Api.Features.Admin.Users;

public sealed record UpdateAdminUserCommand(
    Guid AdminUserId,
    Guid TargetUserId,
    string? Email,
    string? DisplayName,
    string? Password,
    string? Role,
    bool? IsActive) : IRequest<AdminUserResult>;
