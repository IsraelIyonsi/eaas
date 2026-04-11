namespace EaaS.Api.Features.Admin.Users;

public sealed record AdminUserResult(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
