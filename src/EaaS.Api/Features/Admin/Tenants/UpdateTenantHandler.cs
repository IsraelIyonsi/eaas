using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed class UpdateTenantHandler : IRequestHandler<UpdateTenantCommand, TenantDetailResult>
{
    private readonly AppDbContext _dbContext;

    public UpdateTenantHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantDetailResult> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
            throw new NotFoundException("Tenant not found");

        var now = DateTime.UtcNow;

        if (request.Name is not null) tenant.Name = request.Name;
        if (request.ContactEmail is not null) tenant.ContactEmail = request.ContactEmail;
        if (request.CompanyName is not null) tenant.CompanyName = request.CompanyName;
        if (request.MaxApiKeys.HasValue) tenant.MaxApiKeys = request.MaxApiKeys;
        if (request.MaxDomainsCount.HasValue) tenant.MaxDomainsCount = request.MaxDomainsCount;
        if (request.MonthlyEmailLimit.HasValue) tenant.MonthlyEmailLimit = request.MonthlyEmailLimit;
        if (request.Notes is not null) tenant.Notes = request.Notes;

        tenant.UpdatedAt = now;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = request.AdminUserId,
            Action = AuditAction.TenantUpdated,
            TargetType = "Tenant",
            TargetId = tenant.Id.ToString(),
            Details = JsonSerializer.Serialize(new { name = tenant.Name }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var apiKeyCount = await _dbContext.ApiKeys.CountAsync(k => k.TenantId == tenant.Id, cancellationToken);
        var domainCount = await _dbContext.Domains.CountAsync(d => d.TenantId == tenant.Id, cancellationToken);
        var emailCount = await _dbContext.Emails.CountAsync(e => e.TenantId == tenant.Id, cancellationToken);

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
            apiKeyCount, domainCount, emailCount,
            tenant.CreatedAt,
            tenant.UpdatedAt);
    }
}
