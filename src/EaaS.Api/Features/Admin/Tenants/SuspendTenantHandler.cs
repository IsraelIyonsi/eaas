using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed class SuspendTenantHandler : IRequestHandler<SuspendTenantCommand>
{
    private readonly AppDbContext _dbContext;

    public SuspendTenantHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
            throw new NotFoundException("Tenant not found");

        var now = DateTime.UtcNow;
        tenant.Status = TenantStatus.Suspended;
        tenant.UpdatedAt = now;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = request.AdminUserId,
            Action = AuditAction.TenantSuspended,
            TargetType = "Tenant",
            TargetId = tenant.Id.ToString(),
            Details = JsonSerializer.Serialize(new { reason = request.Reason }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        EmailMetrics.AdminOperationsTotal.WithLabels("tenant_suspend").Inc();
    }
}
