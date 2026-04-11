using MediatR;

namespace EaaS.Api.Features.Admin.Health;

public sealed record GetSystemHealthQuery : IRequest<SystemHealthResult>;
