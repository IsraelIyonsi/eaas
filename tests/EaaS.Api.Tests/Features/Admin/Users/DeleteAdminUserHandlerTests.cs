using EaaS.Api.Features.Admin.Users;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Users;

public sealed class DeleteAdminUserHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly DeleteAdminUserHandler _sut;

    public DeleteAdminUserHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new DeleteAdminUserHandler(_dbContext);
    }

    [Fact]
    public async Task Should_DeleteUser_WhenExists()
    {
        var user = TestDataBuilders.AnAdminUser()
            .WithIsActive(true)
            .Build();

        _dbContext.AdminUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        var command = new DeleteAdminUserCommand(Guid.NewGuid(), user.Id);

        await _sut.Handle(command, CancellationToken.None);

        var updated = await _dbContext.AdminUsers.FindAsync(user.Id);
        updated!.IsActive.Should().BeFalse();
        updated.UpdatedAt.Should().BeAfter(user.CreatedAt);
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenUserNotFound()
    {
        var command = new DeleteAdminUserCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_CreateAuditLog()
    {
        var adminUserId = Guid.NewGuid();
        var user = TestDataBuilders.AnAdminUser()
            .WithEmail("deleted@test.com")
            .Build();

        _dbContext.AdminUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        var command = new DeleteAdminUserCommand(adminUserId, user.Id);

        await _sut.Handle(command, CancellationToken.None);

        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == AuditAction.AdminUserDeleted);
        auditLog.Should().NotBeNull();
        auditLog!.AdminUserId.Should().Be(adminUserId);
        auditLog.TargetType.Should().Be("AdminUser");
        auditLog.TargetId.Should().Be(user.Id.ToString());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
