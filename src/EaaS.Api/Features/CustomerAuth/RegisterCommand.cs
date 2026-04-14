using MediatR;

namespace EaaS.Api.Features.CustomerAuth;

public sealed record RegisterCommand(
    string Name,
    string Email,
    string Password,
    string? CompanyName,
    string LegalEntityName,
    string PostalAddress) : IRequest<RegisterResult>;
