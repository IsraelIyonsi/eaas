using MediatR;

namespace EaaS.Api.Features.CustomerAuth;

public sealed record CustomerLoginCommand(
    string Email,
    string Password) : IRequest<CustomerLoginResult>;
