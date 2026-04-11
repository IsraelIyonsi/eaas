using MediatR;

namespace EaaS.Api.Features.Admin.Auth;

public sealed record AdminLoginCommand(
    string Email,
    string Password,
    string IpAddress) : IRequest<AdminLoginResult>;
