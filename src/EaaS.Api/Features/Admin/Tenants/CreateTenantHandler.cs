using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Metrics;
using EaaS.Infrastructure.Persistence;
using MediatR;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed class CreateTenantHandler : IRequestHandler<CreateTenantCommand, TenantDetailResult>
{
    private readonly AppDbContext _dbContext;

    public CreateTenantHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantDetailResult> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Status = TenantStatus.Active,
            ContactEmail = request.ContactEmail,
            CompanyName = request.CompanyName,
            LegalEntityName = request.LegalEntityName,
            PostalAddress = request.PostalAddress,
            MaxApiKeys = request.MaxApiKeys,
            MaxDomainsCount = request.MaxDomainsCount,
            MonthlyEmailLimit = request.MonthlyEmailLimit,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Tenants.Add(tenant);

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = request.AdminUserId,
            Action = AuditAction.TenantCreated,
            TargetType = "Tenant",
            TargetId = tenant.Id.ToString(),
            Details = JsonSerializer.Serialize(new { name = tenant.Name }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        EmailMetrics.AdminOperationsTotal.WithLabels("tenant_create").Inc();

        return new TenantDetailResult(
            tenant.Id,
            tenant.Name,
            tenant.Status.ToString().ToLowerInvariant(),
            tenant.CompanyName,
            tenant.ContactEmail,
            tenant.MaxApiKeys,
            tenant.MaxDomainsCount,
            tenant.MonthlyEmailLimit,
            tenant.Notes,
            0, 0, 0,
            tenant.CreatedAt,
            tenant.UpdatedAt);
    }
}
