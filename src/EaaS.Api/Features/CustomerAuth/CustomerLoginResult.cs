namespace EaaS.Api.Features.CustomerAuth;

public sealed record CustomerLoginResult(
    Guid TenantId,
    string Name,
    string Email);
