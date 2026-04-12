using EaaS.Api.Features.Billing.Subscriptions;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Subscriptions;

public sealed class CancelSubscriptionHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CancelSubscriptionHandler _sut;

    public CancelSubscriptionHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CancelSubscriptionHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CancelAtPeriodEnd()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan().WithName("Starter").Build();
        _dbContext.Plans.Add(plan);

        var periodEnd = DateTime.UtcNow.AddDays(15);
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = periodEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var command = new CancelSubscriptionCommand(tenantId, false);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.CancelledAt.Should().NotBeNull();
        result.Status.Should().Be("active"); // Still active until period end
    }

    [Fact]
    public async Task Should_CancelImmediately_WhenFlagSet()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan().WithName("Pro").Build();
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

        var command = new CancelSubscriptionCommand(tenantId, true);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.CancelledAt.Should().NotBeNull();
        result.Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task Should_ThrowNotFound_WhenNoSubscription()
    {
        var tenantId = Guid.NewGuid();
        var command = new CancelSubscriptionCommand(tenantId, false);
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Should_ThrowValidation_WhenAlreadyCancelled()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan().WithName("Business").Build();
        _dbContext.Plans.Add(plan);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Cancelled,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-30),
            CurrentPeriodEnd = DateTime.UtcNow,
            CancelledAt = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var command = new CancelSubscriptionCommand(tenantId, false);
        var act = () => _sut.Handle(command, CancellationToken.None);

        // After the fix, cancelled subscriptions are excluded from the query,
        // so the handler sees no active subscription and throws NotFoundException.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
