using EaaS.Api.Features.Billing.Plans;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Plans;

public sealed class CreatePlanHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CreatePlanHandler _sut;

    public CreatePlanHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CreatePlanHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CreatePlan_WithAllFields()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithName("Pro Plan")
            .WithTier(PlanTier.Pro)
            .WithMonthlyPriceUsd(29.99m)
            .WithAnnualPriceUsd(299.99m)
            .WithDailyEmailLimit(10000)
            .WithMonthlyEmailLimit(300000)
            .WithMaxApiKeys(10)
            .WithMaxDomains(10)
            .WithMaxTemplates(200)
            .WithMaxWebhooks(25)
            .WithCustomDomainBranding(true)
            .WithPrioritySupport(false)
            .Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Pro Plan");
        result.Tier.Should().Be("pro");
        result.MonthlyPriceUsd.Should().Be(29.99m);
        result.AnnualPriceUsd.Should().Be(299.99m);
        result.DailyEmailLimit.Should().Be(10000);
        result.MonthlyEmailLimit.Should().Be(300000);
        result.MaxApiKeys.Should().Be(10);
        result.MaxDomains.Should().Be(10);
        result.MaxTemplates.Should().Be(200);
        result.MaxWebhooks.Should().Be(25);
        result.CustomDomainBranding.Should().BeTrue();
        result.PrioritySupport.Should().BeFalse();
        result.IsActive.Should().BeTrue();

        var stored = await _dbContext.Plans.FindAsync(result.Id);
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ThrowConflict_WhenNameExists()
    {
        _dbContext.Plans.Add(new Plan
        {
            Id = Guid.NewGuid(),
            Name = "Existing Plan",
            Tier = PlanTier.Free,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var command = TestDataBuilders.CreatePlan()
            .WithName("Existing Plan")
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Should_SetDefaultsForOptionalFields()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithName("Minimal Plan")
            .Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsActive.Should().BeTrue();
        result.CustomDomainBranding.Should().BeFalse();
        result.PrioritySupport.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
