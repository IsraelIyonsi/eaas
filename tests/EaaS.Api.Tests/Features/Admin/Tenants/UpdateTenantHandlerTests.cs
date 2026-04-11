using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Tenants;

public sealed class UpdateTenantHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly UpdateTenantHandler _sut;

    public UpdateTenantHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new UpdateTenantHandler(_dbContext);
    }

    [Fact]
    public async Task Should_UpdateTenantFields()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithName("Old Name")
            .WithCompanyName("Old Corp")
            .Build();

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateTenantCommand(
            Guid.NewGuid(), tenant.Id,
            Name: "New Name",
            ContactEmail: "new@example.com",
            CompanyName: "New Corp",
            MaxApiKeys: 10,
            MaxDomainsCount: 5,
            MonthlyEmailLimit: 50000,
            Notes: "Updated notes");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Name.Should().Be("New Name");
        result.CompanyName.Should().Be("New Corp");
        result.ContactEmail.Should().Be("new@example.com");
        result.MaxApiKeys.Should().Be(10);
        result.MaxDomainsCount.Should().Be(5);
        result.MonthlyEmailLimit.Should().Be(50000);
        result.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenTenantNotFound()
    {
        var command = new UpdateTenantCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            Name: "Name", ContactEmail: null, CompanyName: null,
            MaxApiKeys: null, MaxDomainsCount: null,
            MonthlyEmailLimit: null, Notes: null);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_CreateAuditLog()
    {
        var adminUserId = Guid.NewGuid();
        var tenant = TestDataBuilders.ATenant()
            .WithName("Original")
            .Build();

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateTenantCommand(
            adminUserId, tenant.Id,
            Name: "Updated", ContactEmail: null, CompanyName: null,
            MaxApiKeys: null, MaxDomainsCount: null,
            MonthlyEmailLimit: null, Notes: null);

        await _sut.Handle(command, CancellationToken.None);

        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == AuditAction.TenantUpdated);
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
