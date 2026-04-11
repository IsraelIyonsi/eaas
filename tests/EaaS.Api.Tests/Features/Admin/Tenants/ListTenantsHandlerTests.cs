using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Tenants;

public sealed class ListTenantsHandlerTests
{
    [Fact]
    public async Task Should_ReturnPagedTenants()
    {
        var tenants = new List<Tenant>
        {
            TestDataBuilders.ATenant().WithName("Tenant A").Build(),
            TestDataBuilders.ATenant().WithName("Tenant B").Build(),
            TestDataBuilders.ATenant().WithName("Tenant C").Build(),
        };

        var dbContext = CreateMockDbContext(tenants);
        var sut = new ListTenantsHandler(dbContext);

        var query = new ListTenantsQuery(Page: 1, PageSize: 10, Status: null, Search: null);

        var result = await sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Should_FilterByStatus()
    {
        var tenants = new List<Tenant>
        {
            TestDataBuilders.ATenant().WithName("Active Tenant").WithStatus(TenantStatus.Active).Build(),
            TestDataBuilders.ATenant().WithName("Suspended Tenant").WithStatus(TenantStatus.Suspended).Build(),
            TestDataBuilders.ATenant().WithName("Another Active").WithStatus(TenantStatus.Active).Build(),
        };

        var dbContext = CreateMockDbContext(tenants);
        var sut = new ListTenantsHandler(dbContext);

        var query = new ListTenantsQuery(Page: 1, PageSize: 10, Status: "Suspended", Search: null);

        var result = await sut.Handle(query, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Suspended Tenant");
    }

    [Fact]
    public async Task Should_SearchByName()
    {
        var tenants = new List<Tenant>
        {
            TestDataBuilders.ATenant().WithName("Acme Corp").Build(),
            TestDataBuilders.ATenant().WithName("Beta Inc").Build(),
            TestDataBuilders.ATenant().WithName("Acme Labs").Build(),
        };

        var dbContext = CreateMockDbContext(tenants);
        var sut = new ListTenantsHandler(dbContext);

        var query = new ListTenantsQuery(Page: 1, PageSize: 10, Status: null, Search: "acme");

        var result = await sut.Handle(query, CancellationToken.None);

        result.Total.Should().Be(2);
        result.Items.Should().AllSatisfy(t => t.Name.Should().Contain("Acme"));
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
