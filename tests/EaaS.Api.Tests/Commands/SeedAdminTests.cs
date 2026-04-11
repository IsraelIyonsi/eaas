using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EaaS.Api.Tests.Commands;

public sealed class SeedAdminTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _serviceProvider;

    public SeedAdminTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
    }

    private AppDbContext CreateDbContext()
    {
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [Fact]
    public async Task Should_CreateAdminUser_WithSuperAdminRole()
    {
        var args = new[] { "seed", "--admin", "admin@eaas.io", "StrongPass123!" };

        var exitCode = await EaaS.Api.Commands.SeedCommand.ExecuteAsync(args, _serviceProvider);

        exitCode.Should().Be(0);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(u => u.Email == "admin@eaas.io");
        adminUser.Should().NotBeNull();
        adminUser!.Role.Should().Be(AdminRole.SuperAdmin);
        adminUser.IsActive.Should().BeTrue();
        adminUser.DisplayName.Should().Be("admin@eaas.io");
    }

    [Fact]
    public async Task Should_HashPassword_WithBCrypt()
    {
        var args = new[] { "seed", "--admin", "admin@eaas.io", "StrongPass123!" };

        await EaaS.Api.Commands.SeedCommand.ExecuteAsync(args, _serviceProvider);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(u => u.Email == "admin@eaas.io");
        adminUser.Should().NotBeNull();
        adminUser!.PasswordHash.Should().StartWith("$2");
        BCrypt.Net.BCrypt.Verify("StrongPass123!", adminUser.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Should_RejectDuplicateEmail()
    {
        // Pre-seed an existing admin user
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.AdminUsers.Add(new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = "admin@eaas.io",
                DisplayName = "Existing Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = AdminRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var args = new[] { "seed", "--admin", "admin@eaas.io", "StrongPass123!" };

        var exitCode = await EaaS.Api.Commands.SeedCommand.ExecuteAsync(args, _serviceProvider);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Should_RejectInvalidEmailFormat()
    {
        var args = new[] { "seed", "--admin", "not-an-email", "StrongPass123!" };

        var exitCode = await EaaS.Api.Commands.SeedCommand.ExecuteAsync(args, _serviceProvider);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Should_RejectShortPassword()
    {
        var args = new[] { "seed", "--admin", "admin@eaas.io", "short" };

        var exitCode = await EaaS.Api.Commands.SeedCommand.ExecuteAsync(args, _serviceProvider);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Should_RejectMissingArguments()
    {
        var args = new[] { "seed", "--admin" };

        var exitCode = await EaaS.Api.Commands.SeedCommand.ExecuteAsync(args, _serviceProvider);

        exitCode.Should().Be(1);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
