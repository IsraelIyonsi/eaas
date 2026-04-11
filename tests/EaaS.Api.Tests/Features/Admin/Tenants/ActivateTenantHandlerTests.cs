using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Tenants;

public sealed class ActivateTenantHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ActivateTenantHandler _sut;

    public ActivateTenantHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new ActivateTenantHandler(_dbContext);
    }

    [Fact]
    public async Task Should_ActivateTenant_WhenSuspended()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithStatus(TenantStatus.Suspended)
            .Build();

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new ActivateTenantCommand(Guid.NewGuid(), tenant.Id);

        await _sut.Handle(command, CancellationToken.None);

        var updated = await _dbContext.Tenants.FindAsync(tenant.Id);
        updated!.Status.Should().Be(TenantStatus.Active);
        updated.UpdatedAt.Should().BeAfter(tenant.CreatedAt);
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenTenantNotFound()
    {
        var command = new ActivateTenantCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_CreateAuditLog()
    {
        var adminUserId = Guid.NewGuid();
        var tenant = TestDataBuilders.ATenant()
            .WithStatus(TenantStatus.Suspended)
            .Build();

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new ActivateTenantCommand(adminUserId, tenant.Id);

        await _sut.Handle(command, CancellationToken.None);

        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == AuditAction.TenantActivated);
        auditLog.Should().NotBeNull();
        auditLog!.AdminUserId.Should().Be(adminUserId);
        auditLog.TargetType.Should().Be("Tenant");
        auditLog.TargetId.Should().Be(tenant.Id.ToString());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
