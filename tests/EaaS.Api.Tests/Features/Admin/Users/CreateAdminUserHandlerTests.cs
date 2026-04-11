using EaaS.Api.Features.Admin.Users;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Users;

public sealed class CreateAdminUserHandlerTests
{
    [Fact]
    public async Task Should_CreateUser_WithBcryptHash()
    {
        var adminUsers = new List<AdminUser>();
        var auditLogs = new List<AuditLog>();
        var dbContext = CreateMockDbContext(adminUsers, auditLogs);
        var sut = new CreateAdminUserHandler(dbContext);

        var command = TestDataBuilders.CreateAdminUser()
            .WithEmail("new@test.com")
            .WithDisplayName("New User")
            .WithPassword("SecurePassword123!")
            .WithRole("Admin")
            .Build();

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Email.Should().Be("new@test.com");
        result.DisplayName.Should().Be("New User");
        result.Role.Should().Be("admin");
        result.IsActive.Should().BeTrue();

        adminUsers.Should().ContainSingle();
        BCrypt.Net.BCrypt.Verify("SecurePassword123!", adminUsers[0].PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Should_ThrowConflict_WhenEmailExists()
    {
        var existingUser = TestDataBuilders.AnAdminUser()
            .WithEmail("existing@test.com")
            .Build();
        var adminUsers = new List<AdminUser> { existingUser };
        var auditLogs = new List<AuditLog>();
        var dbContext = CreateMockDbContext(adminUsers, auditLogs);
        var sut = new CreateAdminUserHandler(dbContext);

        var command = TestDataBuilders.CreateAdminUser()
            .WithEmail("existing@test.com")
            .Build();

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Should_CreateAuditLog_WhenCreated()
    {
        var adminUserId = Guid.NewGuid();
        var adminUsers = new List<AdminUser>();
        var auditLogs = new List<AuditLog>();
        var dbContext = CreateMockDbContext(adminUsers, auditLogs);
        var sut = new CreateAdminUserHandler(dbContext);

        var command = TestDataBuilders.CreateAdminUser()
            .WithAdminUserId(adminUserId)
            .WithEmail("audited@test.com")
            .Build();

        await sut.Handle(command, CancellationToken.None);

        auditLogs.Should().ContainSingle(a => a.Action == AuditAction.AdminUserCreated);
        auditLogs[0].AdminUserId.Should().Be(adminUserId);
        auditLogs[0].TargetType.Should().Be("AdminUser");
    }

    private static AppDbContext CreateMockDbContext(
        List<AdminUser> adminUsers,
        List<AuditLog> auditLogs)
    {
        var mockAdminUsers = MockDbSetFactory.Create(adminUsers);
        var mockAuditLogs = MockDbSetFactory.Create(auditLogs);

        var dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        dbContext.AdminUsers.Returns(mockAdminUsers);
        dbContext.AuditLogs.Returns(mockAuditLogs);

        return dbContext;
    }
}
