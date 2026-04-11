using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Tenants;

public sealed class SuspendTenantHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly SuspendTenantHandler _sut;

    public SuspendTenantHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new SuspendTenantHandler(_dbContext);
    }

    [Fact]
    public async Task Should_SuspendActiveTenant()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithStatus(TenantStatus.Active)
            .Build();
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new SuspendTenantCommand(
            AdminUserId: Guid.NewGuid(),
            TenantId: tenant.Id,
            Reason: "Policy violation");

        await _sut.Handle(command, CancellationToken.None);

        var updated = await _dbContext.Tenants.FindAsync(tenant.Id);
        updated!.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenTenantMissing()
    {
        var command = new SuspendTenantCommand(
            AdminUserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            Reason: null);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Tenant not found*");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
