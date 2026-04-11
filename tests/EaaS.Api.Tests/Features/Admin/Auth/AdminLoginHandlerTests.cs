using EaaS.Api.Features.Admin.Auth;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Auth;

public sealed class AdminLoginHandlerTests
{
    private readonly ILogger<AdminLoginHandler> _logger = Substitute.For<ILogger<AdminLoginHandler>>();

    [Fact]
    public async Task Should_ReturnResult_WhenValidCredentials()
    {
        var password = "SecurePass123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var adminUser = TestDataBuilders.AnAdminUser()
            .WithEmail("admin@test.com")
            .WithPasswordHash(hash)
            .WithIsActive(true)
            .Build();

        var dbContext = CreateMockDbContext([adminUser]);
        var sut = new AdminLoginHandler(dbContext, _logger);

        var command = TestDataBuilders.AdminLogin()
            .WithEmail("admin@test.com")
            .WithPassword(password)
            .Build();

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.UserId.Should().Be(adminUser.Id);
        result.Email.Should().Be("admin@test.com");
        result.Role.Should().Be("SuperAdmin");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenInvalidPassword()
    {
        var adminUser = TestDataBuilders.AnAdminUser()
            .WithEmail("admin@test.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword("CorrectPassword"))
            .WithIsActive(true)
            .Build();

        var dbContext = CreateMockDbContext([adminUser]);
        var sut = new AdminLoginHandler(dbContext, _logger);

        var command = TestDataBuilders.AdminLogin()
            .WithEmail("admin@test.com")
            .WithPassword("WrongPassword!")
            .Build();

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenUserNotFound()
    {
        var dbContext = CreateMockDbContext([]);
        var sut = new AdminLoginHandler(dbContext, _logger);

        var command = TestDataBuilders.AdminLogin()
            .WithEmail("nonexistent@test.com")
            .WithPassword("AnyPassword!")
            .Build();

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenUserInactive()
    {
        var password = "SecurePass123!";
        var adminUser = TestDataBuilders.AnAdminUser()
            .WithEmail("inactive@test.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword(password))
            .WithIsActive(false)
            .Build();

        var dbContext = CreateMockDbContext([adminUser]);
        var sut = new AdminLoginHandler(dbContext, _logger);

        var command = TestDataBuilders.AdminLogin()
            .WithEmail("inactive@test.com")
            .WithPassword(password)
            .Build();

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Account is deactivated*");
    }

    [Fact]
    public async Task Should_UpdateLastLoginAt_OnSuccess()
    {
        var password = "SecurePass123!";
        var adminUser = TestDataBuilders.AnAdminUser()
            .WithEmail("admin@test.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword(password))
            .WithIsActive(true)
            .WithLastLoginAt(null)
            .Build();

        var dbContext = CreateMockDbContext([adminUser]);
        var sut = new AdminLoginHandler(dbContext, _logger);

        var command = TestDataBuilders.AdminLogin()
            .WithEmail("admin@test.com")
            .WithPassword(password)
            .Build();

        await sut.Handle(command, CancellationToken.None);

        adminUser.LastLoginAt.Should().NotBeNull();
        adminUser.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_CreateAuditLog_OnSuccess()
    {
        var password = "SecurePass123!";
        var adminUser = TestDataBuilders.AnAdminUser()
            .WithEmail("admin@test.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword(password))
            .WithIsActive(true)
            .Build();

        var auditLogs = new List<AuditLog>();
        var dbContext = CreateMockDbContext([adminUser], auditLogs);
        var sut = new AdminLoginHandler(dbContext, _logger);

        var command = TestDataBuilders.AdminLogin()
            .WithEmail("admin@test.com")
            .WithPassword(password)
            .Build();

        await sut.Handle(command, CancellationToken.None);

        auditLogs.Should().ContainSingle(a => a.Action == AuditAction.AdminLogin);
        auditLogs[0].IpAddress.Should().Be("127.0.0.1");
    }

    private static AppDbContext CreateMockDbContext(
        List<AdminUser> adminUsers,
        List<AuditLog>? auditLogs = null)
    {
        auditLogs ??= [];

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
