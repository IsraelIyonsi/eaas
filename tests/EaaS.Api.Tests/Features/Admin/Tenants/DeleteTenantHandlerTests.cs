using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Tenants;

public sealed class DeleteTenantHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly DeleteTenantHandler _sut;

    public DeleteTenantHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new DeleteTenantHandler(_dbContext);
    }

    [Fact]
    public async Task Should_SoftDeleteTenant()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithName("To Delete")
            .WithStatus(TenantStatus.Active)
            .Build();
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new DeleteTenantCommand(
            AdminUserId: Guid.NewGuid(),
            TenantId: tenant.Id);

        await _sut.Handle(command, CancellationToken.None);

        var updated = await _dbContext.Tenants.FindAsync(tenant.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(TenantStatus.Deactivated);
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenTenantMissing()
    {
        var command = new DeleteTenantCommand(
            AdminUserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Tenant not found*");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
