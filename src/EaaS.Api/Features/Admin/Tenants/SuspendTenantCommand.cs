using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed record SuspendTenantCommand(
    Guid AdminUserId,
    Guid TenantId,
    string? Reason) : IRequest;
