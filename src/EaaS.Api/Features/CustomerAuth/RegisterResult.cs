namespace EaaS.Api.Features.CustomerAuth;

public sealed record RegisterResult(
    Guid TenantId,
    string Name,
    string Email,
    string ApiKey);
