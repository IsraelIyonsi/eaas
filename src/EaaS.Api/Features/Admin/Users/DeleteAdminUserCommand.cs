using MediatR;

namespace EaaS.Api.Features.Admin.Users;

public sealed record DeleteAdminUserCommand(
    Guid AdminUserId,
    Guid TargetUserId) : IRequest;
