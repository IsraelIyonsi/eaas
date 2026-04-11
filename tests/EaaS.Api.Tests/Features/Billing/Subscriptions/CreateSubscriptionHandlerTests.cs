using EaaS.Api.Features.Billing.Subscriptions;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Subscriptions;

public sealed class CreateSubscriptionHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CreateSubscriptionHandler _sut;

    public CreateSubscriptionHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CreateSubscriptionHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CreateSubscription_WithFreePlan()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan()
            .WithName("Free")
            .WithTier(PlanTier.Free)
            .WithMonthlyPriceUsd(0m)
            .Build();

        _dbContext.Plans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var command = new CreateSubscriptionCommand(tenantId, plan.Id, null);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("active");
        result.PlanName.Should().Be("Free");
        result.TrialEndsAt.Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowConflict_WhenAlreadySubscribed()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan().WithName("Starter").Build();

        _dbContext.Plans.Add(plan);
        _dbContext.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var command = new CreateSubscriptionCommand(tenantId, plan.Id, null);
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenPlanNotFound()
    {
        var tenantId = Guid.NewGuid();
        var command = new CreateSubscriptionCommand(tenantId, Guid.NewGuid(), null);
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_SetTrialPeriod_ForPaidPlans()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan()
            .WithName("Pro")
            .WithTier(PlanTier.Pro)
            .WithMonthlyPriceUsd(29.99m)
            .Build();

        _dbContext.Plans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var command = new CreateSubscriptionCommand(tenantId, plan.Id, null);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("trial");
        result.TrialEndsAt.Should().NotBeNull();
        result.TrialEndsAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(14), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_SetFreeDefaults_ForFreePlan()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan()
            .WithName("Free Tier")
            .WithTier(PlanTier.Free)
            .WithMonthlyPriceUsd(0m)
            .Build();

        _dbContext.Plans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var command = new CreateSubscriptionCommand(tenantId, plan.Id, null);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("active");
        result.TrialEndsAt.Should().BeNull();
        result.Provider.Should().Be("none");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
