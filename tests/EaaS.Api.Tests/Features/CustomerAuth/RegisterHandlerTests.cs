using EaaS.Api.Features.CustomerAuth;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.CustomerAuth;

public sealed class RegisterHandlerTests
{
    private readonly ILogger<RegisterHandler> _logger = Substitute.For<ILogger<RegisterHandler>>();

    [Fact]
    public async Task Should_RegisterTenant_WithBCryptHash()
    {
        var tenants = new List<Tenant>();
        var apiKeys = new List<ApiKey>();
        var dbContext = CreateMockDbContext(tenants, apiKeys);
        var sut = new RegisterHandler(dbContext, _logger);

        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass1", "Acme Corp");

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TenantId.Should().NotBeEmpty();
        result.Name.Should().Be("John Doe");
        result.Email.Should().Be("john@example.com");
        result.ApiKey.Should().NotBeNullOrEmpty();

        tenants.Should().ContainSingle();
        var tenant = tenants[0];
        tenant.PasswordHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify("SecurePass1", tenant.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Should_ThrowConflict_WhenEmailExists()
    {
        var existingTenant = TestDataBuilders.ATenant()
            .WithContactEmail("existing@example.com")
            .Build();

        var tenants = new List<Tenant> { existingTenant };
        var apiKeys = new List<ApiKey>();
        var dbContext = CreateMockDbContext(tenants, apiKeys);
        var sut = new RegisterHandler(dbContext, _logger);

        var command = new RegisterCommand("Jane Doe", "existing@example.com", "SecurePass1", null);

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Should_GenerateApiKey_WithCorrectPrefix()
    {
        var tenants = new List<Tenant>();
        var apiKeys = new List<ApiKey>();
        var dbContext = CreateMockDbContext(tenants, apiKeys);
        var sut = new RegisterHandler(dbContext, _logger);

        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass1", null);

        var result = await sut.Handle(command, CancellationToken.None);

        result.ApiKey.Should().StartWith("snx_live_");
        apiKeys.Should().ContainSingle();
        apiKeys[0].KeyHash.Should().NotBeNullOrEmpty();
        apiKeys[0].Prefix.Should().Be(result.ApiKey[..8]);
    }

    [Fact]
    public async Task Should_SetFreeTierDefaults()
    {
        var tenants = new List<Tenant>();
        var apiKeys = new List<ApiKey>();
        var dbContext = CreateMockDbContext(tenants, apiKeys);
        var sut = new RegisterHandler(dbContext, _logger);

        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass1", null);

        await sut.Handle(command, CancellationToken.None);

        tenants.Should().ContainSingle();
        var tenant = tenants[0];
        tenant.MonthlyEmailLimit.Should().Be(3000);
        tenant.MaxApiKeys.Should().Be(3);
        tenant.MaxDomainsCount.Should().Be(2);
        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Should_SetCompanyName_WhenProvided()
    {
        var tenants = new List<Tenant>();
        var apiKeys = new List<ApiKey>();
        var dbContext = CreateMockDbContext(tenants, apiKeys);
        var sut = new RegisterHandler(dbContext, _logger);

        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass1", "Acme Corp");

        await sut.Handle(command, CancellationToken.None);

        tenants[0].CompanyName.Should().Be("Acme Corp");
    }

    private static AppDbContext CreateMockDbContext(
        List<Tenant> tenants,
        List<ApiKey> apiKeys)
    {
        var mockTenants = MockDbSetFactory.Create(tenants);
        var mockApiKeys = MockDbSetFactory.Create(apiKeys);

        var dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        dbContext.Tenants.Returns(mockTenants);
        dbContext.ApiKeys.Returns(mockApiKeys);

        return dbContext;
    }
}
