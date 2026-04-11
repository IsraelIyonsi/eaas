using EaaS.Api.Features.Billing.Plans;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Plans;

public sealed class ListPlansHandlerTests
{
    [Fact]
    public async Task Should_ReturnAllActivePlans()
    {
        var plans = new List<Plan>
        {
            TestDataBuilders.APlan().WithName("Free").WithTier(PlanTier.Free).Build(),
            TestDataBuilders.APlan().WithName("Starter").WithTier(PlanTier.Starter).Build(),
            TestDataBuilders.APlan().WithName("Pro").WithTier(PlanTier.Pro).Build(),
        };

        var dbContext = CreateMockDbContext(plans);
        var sut = new ListPlansHandler(dbContext);

        var query = new ListPlansQuery();

        var result = await sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Should_ReturnEmptyList_WhenNoPlans()
    {
        var plans = new List<Plan>();

        var dbContext = CreateMockDbContext(plans);
        var sut = new ListPlansHandler(dbContext);

        var query = new ListPlansQuery();

        var result = await sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ExcludeInactivePlans()
    {
        var plans = new List<Plan>
        {
            TestDataBuilders.APlan().WithName("Active Plan").WithIsActive(true).Build(),
            TestDataBuilders.APlan().WithName("Inactive Plan").WithIsActive(false).Build(),
        };

        var dbContext = CreateMockDbContext(plans);
        var sut = new ListPlansHandler(dbContext);

        var query = new ListPlansQuery();

        var result = await sut.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Active Plan");
    }

    private static AppDbContext CreateMockDbContext(List<Plan> plans)
    {
        var mockPlans = MockDbSetFactory.Create(plans);

        var dbContext = Substitute.For<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        dbContext.Plans.Returns(mockPlans);

        return dbContext;
    }
}
