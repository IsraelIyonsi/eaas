namespace EaaS.Api.Features.Admin.Auth;

public sealed record AdminLoginResult(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role);
