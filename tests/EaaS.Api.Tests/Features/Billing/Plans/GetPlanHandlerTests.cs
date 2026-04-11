using EaaS.Api.Features.Billing.Plans;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Plans;

public sealed class GetPlanHandlerTests
{
    [Fact]
    public async Task Should_ReturnPlan_WhenExists()
    {
        var planId = Guid.NewGuid();
        var plans = new List<Plan>
        {
            TestDataBuilders.APlan()
                .WithId(planId)
                .WithName("Pro")
                .WithTier(PlanTier.Pro)
                .WithMonthlyPriceUsd(29.99m)
                .Build(),
        };

        var dbContext = CreateMockDbContext(plans);
        var sut = new GetPlanHandler(dbContext);

        var query = new GetPlanQuery(planId);

        var result = await sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(planId);
        result.Name.Should().Be("Pro");
        result.Tier.Should().Be("pro");
        result.MonthlyPriceUsd.Should().Be(29.99m);
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenPlanDoesNotExist()
    {
        var plans = new List<Plan>();

        var dbContext = CreateMockDbContext(plans);
        var sut = new GetPlanHandler(dbContext);

        var query = new GetPlanQuery(Guid.NewGuid());

        var act = () => sut.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
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
