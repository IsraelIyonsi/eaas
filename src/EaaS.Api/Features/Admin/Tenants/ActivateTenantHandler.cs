using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed class ActivateTenantHandler : IRequestHandler<ActivateTenantCommand>
{
    private readonly AppDbContext _dbContext;

    public ActivateTenantHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(ActivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
            throw new NotFoundException("Tenant not found");

        var now = DateTime.UtcNow;
        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = now;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = request.AdminUserId,
            Action = AuditAction.TenantActivated,
            TargetType = "Tenant",
            TargetId = tenant.Id.ToString(),
            Details = "{}",
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        EmailMetrics.AdminOperationsTotal.WithLabels("tenant_activate").Inc();
    }
}
