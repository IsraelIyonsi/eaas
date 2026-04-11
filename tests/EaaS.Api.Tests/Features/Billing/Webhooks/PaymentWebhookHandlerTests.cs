using EaaS.Api.Features.Billing.Webhooks;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Webhooks;

public sealed class PaymentWebhookHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IPaymentProviderFactory _paymentFactory;
    private readonly IPaymentProvider _mockProvider;
    private readonly ProcessPaymentWebhookHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public PaymentWebhookHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _paymentFactory = Substitute.For<IPaymentProviderFactory>();
        _mockProvider = Substitute.For<IPaymentProvider>();
        _paymentFactory.GetProvider(Arg.Any<PaymentProvider>()).Returns(_mockProvider);
        var logger = Substitute.For<ILogger<ProcessPaymentWebhookHandler>>();
        _sut = new ProcessPaymentWebhookHandler(_dbContext, _paymentFactory, logger);
    }

    [Fact]
    public async Task Should_ProcessPayStackChargeSuccess_UpdateInvoice()
    {
        // Arrange
        var plan = SeedPlan();
        var subscription = SeedSubscription(_tenantId, plan.Id, SubscriptionStatus.Active);
        var invoice = SeedInvoice(_tenantId, subscription.Id, status: InvoiceStatus.Pending, externalPaymentId: "pay_123");

        _mockProvider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookEvent(
                "charge.success",
                "pay_123",
                null,
                subscription.ExternalSubscriptionId,
                new Dictionary<string, object>()));

        var command = new ProcessPaymentWebhookCommand("PayStack", "{}", "valid-sig");

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        var updatedInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
        updatedInvoice!.Status.Should().Be(InvoiceStatus.Paid);
        updatedInvoice.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ProcessPayStackSubscriptionDisable_CancelSubscription()
    {
        // Arrange
        var plan = SeedPlan();
        var subscription = SeedSubscription(_tenantId, plan.Id, SubscriptionStatus.Active,
            externalSubscriptionId: "sub_456");

        _mockProvider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookEvent(
                "subscription.not_renew",
                "sub_456",
                null,
                "sub_456",
                new Dictionary<string, object>()));

        var command = new ProcessPaymentWebhookCommand("PayStack", "{}", "valid-sig");

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        var updatedSub = await _dbContext.Subscriptions.FindAsync(subscription.Id);
        updatedSub!.Status.Should().Be(SubscriptionStatus.Cancelled);
        updatedSub.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_RejectInvalidSignature()
    {
        // Arrange: provider returns null for invalid signature
        _mockProvider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((WebhookEvent?)null);

        var command = new ProcessPaymentWebhookCommand("PayStack", "{}", "invalid-sig");

        // Act
        var act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Should_IgnoreUnknownEventType()
    {
        // Arrange
        _mockProvider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookEvent(
                "unknown.event.type",
                "ext_999",
                null,
                null,
                new Dictionary<string, object>()));

        var command = new ProcessPaymentWebhookCommand("PayStack", "{}", "valid-sig");

        // Act -- should not throw
        var act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_BeIdempotent_DuplicateEventId()
    {
        // Arrange: process the same charge.success twice
        var plan = SeedPlan();
        var subscription = SeedSubscription(_tenantId, plan.Id, SubscriptionStatus.Active);
        var invoice = SeedInvoice(_tenantId, subscription.Id, status: InvoiceStatus.Pending, externalPaymentId: "pay_dup");

        _mockProvider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookEvent(
                "charge.success",
                "pay_dup",
                null,
                subscription.ExternalSubscriptionId,
                new Dictionary<string, object>()));

        var command = new ProcessPaymentWebhookCommand("PayStack", "{}", "valid-sig");

        // Act: process twice
        await _sut.Handle(command, CancellationToken.None);
        await _sut.Handle(command, CancellationToken.None);

        // Assert: invoice still paid, no error
        var updatedInvoice = await _dbContext.Invoices.FindAsync(invoice.Id);
        updatedInvoice!.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Should_SyncTenantLimits_OnSubscriptionActivation()
    {
        // Arrange
        var plan = SeedPlan(monthlyEmailLimit: 100_000, maxApiKeys: 20, maxDomains: 10);
        var subscription = SeedSubscription(_tenantId, plan.Id, SubscriptionStatus.Trial,
            externalSubscriptionId: "sub_activate");
        SeedTenant(_tenantId);

        _mockProvider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookEvent(
                "subscription.create",
                "sub_activate",
                null,
                "sub_activate",
                new Dictionary<string, object>()));

        var command = new ProcessPaymentWebhookCommand("PayStack", "{}", "valid-sig");

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert: subscription is now active
        var updatedSub = await _dbContext.Subscriptions.FindAsync(subscription.Id);
        updatedSub!.Status.Should().Be(SubscriptionStatus.Active);

        // Assert: tenant limits synced from plan
        var tenant = await _dbContext.Tenants.FindAsync(_tenantId);
        tenant!.MonthlyEmailLimit.Should().Be(100_000);
        tenant.MaxApiKeys.Should().Be(20);
        tenant.MaxDomainsCount.Should().Be(10);
    }

    // --- Helpers ---

    private Plan SeedPlan(long monthlyEmailLimit = 50_000, int maxApiKeys = 10, int maxDomains = 5)
    {
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            Tier = PlanTier.Pro,
            MonthlyPriceUsd = 29.99m,
            AnnualPriceUsd = 299.99m,
            DailyEmailLimit = 5000,
            MonthlyEmailLimit = monthlyEmailLimit,
            MaxApiKeys = maxApiKeys,
            MaxDomains = maxDomains,
            MaxTemplates = 50,
            MaxWebhooks = 20,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Plans.Add(plan);
        _dbContext.SaveChanges();
        return plan;
    }

    private Subscription SeedSubscription(Guid tenantId, Guid planId, SubscriptionStatus status,
        string externalSubscriptionId = "sub_test_123")
    {
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Status = status,
            Provider = PaymentProvider.PayStack,
            ExternalSubscriptionId = externalSubscriptionId,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Subscriptions.Add(subscription);
        _dbContext.SaveChanges();
        return subscription;
    }

    private Invoice SeedInvoice(Guid tenantId, Guid subscriptionId, InvoiceStatus status, string externalPaymentId)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
            AmountUsd = 29.99m,
            Currency = "USD",
            Status = status,
            Provider = PaymentProvider.PayStack,
            ExternalPaymentId = externalPaymentId,
            PeriodStart = DateTime.UtcNow.AddDays(-30),
            PeriodEnd = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Invoices.Add(invoice);
        _dbContext.SaveChanges();
        return invoice;
    }

    private void SeedTenant(Guid tenantId)
    {
        _dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
