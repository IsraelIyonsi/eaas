using EaaS.Api.Features.PasswordReset;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.PasswordReset;

public sealed class ResetPasswordHandlerTests
{
    private readonly ILogger<ResetPasswordHandler> _logger = Substitute.For<ILogger<ResetPasswordHandler>>();
    private readonly InMemoryPasswordResetTokenStore _tokenStore = new();

    [Fact]
    public async Task Should_UpdatePasswordHash_WhenTokenValid()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("user@example.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword("OldPassword1"))
            .Build();

        var token = "raw-token-xyz";
        var hash = PasswordResetTokenService.HashTokenForStorage(token);
        await _tokenStore.StoreTokenAsync(hash, tenant.Id, tenant.ContactEmail!, TimeSpan.FromMinutes(30));

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        var sut = new ResetPasswordHandler(dbContext, _tokenStore, _logger);

        var result = await sut.Handle(new ResetPasswordCommand(token, "NewPassword1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("NewPassword1", tenant.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Should_ConsumeToken_AfterSuccessfulReset()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("user@example.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword("OldPassword1"))
            .Build();

        var token = "raw-token-abc";
        var hash = PasswordResetTokenService.HashTokenForStorage(token);
        await _tokenStore.StoreTokenAsync(hash, tenant.Id, tenant.ContactEmail!, TimeSpan.FromMinutes(30));

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        var sut = new ResetPasswordHandler(dbContext, _tokenStore, _logger);

        await sut.Handle(new ResetPasswordCommand(token, "NewPassword1"), CancellationToken.None);

        _tokenStore.Tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenTokenAlreadyConsumed()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("user@example.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword("OldPassword1"))
            .Build();

        var token = "raw-token-once";
        var hash = PasswordResetTokenService.HashTokenForStorage(token);
        await _tokenStore.StoreTokenAsync(hash, tenant.Id, tenant.ContactEmail!, TimeSpan.FromMinutes(30));

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        var sut = new ResetPasswordHandler(dbContext, _tokenStore, _logger);

        // First use succeeds.
        await sut.Handle(new ResetPasswordCommand(token, "NewPassword1"), CancellationToken.None);

        // Second use must fail.
        var act = () => sut.Handle(new ResetPasswordCommand(token, "AnotherNew1"), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*invalid or has expired*");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenTokenExpired()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("user@example.com")
            .Build();

        var token = "raw-token-expired";
        var hash = PasswordResetTokenService.HashTokenForStorage(token);
        await _tokenStore.StoreTokenAsync(hash, tenant.Id, tenant.ContactEmail!, TimeSpan.FromMinutes(30));
        _tokenStore.ExpireToken(hash);

        var dbContext = CreateMockDbContext(new List<Tenant> { tenant });
        var sut = new ResetPasswordHandler(dbContext, _tokenStore, _logger);

        var act = () => sut.Handle(new ResetPasswordCommand(token, "NewPassword1"), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenTokenUnknown()
    {
        var dbContext = CreateMockDbContext(new List<Tenant>());
        var sut = new ResetPasswordHandler(dbContext, _tokenStore, _logger);

        var act = () => sut.Handle(new ResetPasswordCommand("bogus", "NewPassword1"), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static AppDbContext CreateMockDbContext(List<Tenant> tenants)
    {
        var mockTenants = MockDbSetFactory.Create(tenants);

        var dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        dbContext.Tenants.Returns(mockTenants);
        return dbContext;
    }
}
