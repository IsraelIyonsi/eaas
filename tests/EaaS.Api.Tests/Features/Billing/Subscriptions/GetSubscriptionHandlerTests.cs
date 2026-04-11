using EaaS.Api.Features.Billing.Subscriptions;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Subscriptions;

public sealed class GetSubscriptionHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly GetSubscriptionHandler _sut;

    public GetSubscriptionHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new GetSubscriptionHandler(_dbContext);
    }

    [Fact]
    public async Task Should_ReturnActiveSubscription_ForTenant()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan()
            .WithName("Starter Plan")
            .WithTier(PlanTier.Starter)
            .WithMonthlyPriceUsd(9.99m)
            .Build();

        _dbContext.Plans.Add(plan);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var query = new GetSubscriptionQuery(tenantId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(subscription.Id);
        result.PlanId.Should().Be(plan.Id);
        result.PlanName.Should().Be("Starter Plan");
        result.Status.Should().Be("active");
        result.Provider.Should().Be("paystack");
    }

    [Fact]
    public async Task Should_ReturnNull_WhenNoSubscription()
    {
        var tenantId = Guid.NewGuid();

        var query = new GetSubscriptionQuery(tenantId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_IncludePlanDetails()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan()
            .WithName("Pro Plan")
            .WithTier(PlanTier.Pro)
            .WithMonthlyPriceUsd(29.99m)
            .Build();

        _dbContext.Plans.Add(plan);

        var now = DateTime.UtcNow;
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Trial,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(30),
            TrialEndsAt = now.AddDays(14),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var query = new GetSubscriptionQuery(tenantId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.PlanName.Should().Be("Pro Plan");
        result.PlanTier.Should().Be("pro");
        result.TrialEndsAt.Should().BeCloseTo(now.AddDays(14), TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
