using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed record DeleteTenantCommand(
    Guid AdminUserId,
    Guid TenantId) : IRequest;
