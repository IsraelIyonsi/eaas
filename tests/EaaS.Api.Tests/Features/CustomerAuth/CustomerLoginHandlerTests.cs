using EaaS.Api.Features.CustomerAuth;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.CustomerAuth;

public sealed class CustomerLoginHandlerTests
{
    private readonly ILogger<CustomerLoginHandler> _logger = Substitute.For<ILogger<CustomerLoginHandler>>();

    [Fact]
    public async Task Should_ReturnResult_WhenCredentialsValid()
    {
        var password = "SecurePass1";
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("john@example.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword(password))
            .WithStatus(TenantStatus.Active)
            .Build();

        var tenants = new List<Tenant> { tenant };
        var dbContext = CreateMockDbContext(tenants);
        var sut = new CustomerLoginHandler(dbContext, _logger);

        var command = new CustomerLoginCommand("john@example.com", password);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenant.Id);
        result.Name.Should().Be(tenant.Name);
        result.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenEmailNotFound()
    {
        var tenants = new List<Tenant>();
        var dbContext = CreateMockDbContext(tenants);
        var sut = new CustomerLoginHandler(dbContext, _logger);

        var command = new CustomerLoginCommand("nonexistent@example.com", "password");

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenPasswordIncorrect()
    {
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("john@example.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword("CorrectPassword"))
            .WithStatus(TenantStatus.Active)
            .Build();

        var tenants = new List<Tenant> { tenant };
        var dbContext = CreateMockDbContext(tenants);
        var sut = new CustomerLoginHandler(dbContext, _logger);

        var command = new CustomerLoginCommand("john@example.com", "WrongPassword");

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenTenantSuspended()
    {
        var password = "SecurePass1";
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("john@example.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword(password))
            .WithStatus(TenantStatus.Suspended)
            .Build();

        var tenants = new List<Tenant> { tenant };
        var dbContext = CreateMockDbContext(tenants);
        var sut = new CustomerLoginHandler(dbContext, _logger);

        var command = new CustomerLoginCommand("john@example.com", password);

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*suspended*");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenTenantDeactivated()
    {
        var password = "SecurePass1";
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("john@example.com")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword(password))
            .WithStatus(TenantStatus.Deactivated)
            .Build();

        var tenants = new List<Tenant> { tenant };
        var dbContext = CreateMockDbContext(tenants);
        var sut = new CustomerLoginHandler(dbContext, _logger);

        var command = new CustomerLoginCommand("john@example.com", password);

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*suspended*");
    }

    [Fact]
    public async Task Should_MatchEmail_CaseInsensitive()
    {
        var password = "SecurePass1";
        var tenant = TestDataBuilders.ATenant()
            .WithContactEmail("John@Example.COM")
            .WithPasswordHash(BCrypt.Net.BCrypt.HashPassword(password))
            .WithStatus(TenantStatus.Active)
            .Build();

        var tenants = new List<Tenant> { tenant };
        var dbContext = CreateMockDbContext(tenants);
        var sut = new CustomerLoginHandler(dbContext, _logger);

        var command = new CustomerLoginCommand("john@example.com", password);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenant.Id);
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
