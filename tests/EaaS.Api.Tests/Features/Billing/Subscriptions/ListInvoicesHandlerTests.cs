using EaaS.Api.Features.Billing.Subscriptions;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Subscriptions;

public sealed class ListInvoicesHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ListInvoicesHandler _sut;

    public ListInvoicesHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new ListInvoicesHandler(_dbContext);
    }

    [Fact]
    public async Task Should_ReturnInvoices_ForTenant()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan().WithName("Pro Invoice").Build();
        _dbContext.Plans.Add(plan);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-30),
            CurrentPeriodEnd = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            TenantId = tenantId,
            InvoiceNumber = "INV-001",
            AmountUsd = 29.99m,
            Currency = "USD",
            Status = InvoiceStatus.Paid,
            Provider = PaymentProvider.PayStack,
            PeriodStart = DateTime.UtcNow.AddDays(-30),
            PeriodEnd = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow.AddDays(-29),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();

        var query = new ListInvoicesQuery(tenantId, 1, 20);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].InvoiceNumber.Should().Be("INV-001");
        result.Items[0].AmountUsd.Should().Be(29.99m);
        result.Items[0].Status.Should().Be("paid");
    }

    [Fact]
    public async Task Should_ReturnEmpty_WhenNoInvoices()
    {
        var tenantId = Guid.NewGuid();
        var query = new ListInvoicesQuery(tenantId, 1, 20);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PaginateResults()
    {
        var tenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan().WithName("Paginated").Build();
        _dbContext.Plans.Add(plan);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-60),
            CurrentPeriodEnd = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);

        for (var i = 0; i < 5; i++)
        {
            _dbContext.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                SubscriptionId = subscription.Id,
                TenantId = tenantId,
                InvoiceNumber = $"INV-{i + 1:D3}",
                AmountUsd = 29.99m,
                Currency = "USD",
                Status = InvoiceStatus.Paid,
                Provider = PaymentProvider.PayStack,
                PeriodStart = DateTime.UtcNow.AddDays(-30 * (i + 1)),
                PeriodEnd = DateTime.UtcNow.AddDays(-30 * i),
                CreatedAt = DateTime.UtcNow.AddDays(-30 * i)
            });
        }

        await _dbContext.SaveChangesAsync();

        var query = new ListInvoicesQuery(tenantId, 1, 2);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Should_NotReturnOtherTenantInvoices()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var plan = TestDataBuilders.APlan().WithName("Isolation").Build();
        _dbContext.Plans.Add(plan);

        var sub1 = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(sub1);

        _dbContext.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SubscriptionId = sub1.Id,
            TenantId = otherTenantId,
            InvoiceNumber = "INV-OTHER",
            AmountUsd = 9.99m,
            Currency = "USD",
            Status = InvoiceStatus.Paid,
            Provider = PaymentProvider.PayStack,
            PeriodStart = DateTime.UtcNow.AddDays(-30),
            PeriodEnd = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        var query = new ListInvoicesQuery(tenantId, 1, 20);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
