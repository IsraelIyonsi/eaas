using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Tenants;

public sealed class CreateTenantHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CreateTenantHandler _sut;

    public CreateTenantHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CreateTenantHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CreateTenant_WhenValid()
    {
        var command = TestDataBuilders.CreateTenant()
            .WithName("New Tenant")
            .WithCompanyName("Test Corp")
            .WithContactEmail("contact@test.com")
            .Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("New Tenant");
        result.Status.Should().Be("active");
        result.CompanyName.Should().Be("Test Corp");
        result.ContactEmail.Should().Be("contact@test.com");

        var stored = await _dbContext.Tenants.FindAsync(result.Id);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Should_CreateAuditLog_WhenCreated()
    {
        var adminUserId = Guid.NewGuid();
        var command = TestDataBuilders.CreateTenant()
            .WithAdminUserId(adminUserId)
            .WithName("Audited Tenant")
            .Build();

        await _sut.Handle(command, CancellationToken.None);

        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == AuditAction.TenantCreated);
        auditLog.Should().NotBeNull();
        auditLog!.AdminUserId.Should().Be(adminUserId);
        auditLog.TargetType.Should().Be("Tenant");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
