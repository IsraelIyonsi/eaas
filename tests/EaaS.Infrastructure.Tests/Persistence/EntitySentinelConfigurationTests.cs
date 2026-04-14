using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EaaS.Infrastructure.Tests.Persistence;

/// <summary>
/// Regression tests for MED-2 (Gate 5 QA).
///
/// EF Core emits <c>BoolWithDefaultWarning</c> (and the equivalent for enums) when a
/// property has a DB-generated default but the CLR default of the property type equals
/// the configured sentinel. In that case, a caller who explicitly assigns the CLR-default
/// value (e.g. <c>InboundRuleAction.Webhook</c> == 0) will have their value silently
/// overwritten by the DB default on insert.
///
/// The fix is to call <see cref="Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.HasSentinel"/>
/// with the same value used for <c>HasDefaultValue</c>. These tests lock in that contract
/// at the EF Core model level, which is provider-agnostic (so InMemory suffices).
/// </summary>
public sealed class EntitySentinelConfigurationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void InboundRule_Action_Should_HaveSentinelMatchingDbDefault()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(InboundRule))!;
        var property = entity.FindProperty(nameof(InboundRule.Action))!;

        property.GetDefaultValue().Should().Be(InboundRuleAction.Store,
            "DB default is declared as Store");
        property.Sentinel.Should().Be(InboundRuleAction.Store,
            "sentinel must equal the DB default so that explicitly-set CLR values " +
            "(including Webhook == 0) are never silently overwritten");
    }

    [Fact]
    public void Subscription_Provider_Should_HaveSentinelMatchingDbDefault()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(Subscription))!;
        var property = entity.FindProperty(nameof(Subscription.Provider))!;

        property.GetDefaultValue().Should().Be(PaymentProvider.Stripe,
            "DB default is declared as Stripe");
        property.Sentinel.Should().Be(PaymentProvider.Stripe,
            "sentinel must equal the DB default so that explicitly-set CLR values " +
            "(including None == 0) are never silently overwritten");
    }

    [Fact]
    public async Task InboundRule_Should_PersistExplicitAction_EvenWhenItEqualsClrDefault()
    {
        // Webhook is the CLR default (enum value 0). Before the fix, setting Action = Webhook
        // would cause EF to treat the property as "unset" and the DB default (Store) would win.
        using var ctx = CreateContext();

        var domain = new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DomainName = "test.example.com",
        };
        ctx.Domains.Add(domain);

        var rule = new InboundRule
        {
            Id = Guid.NewGuid(),
            TenantId = domain.TenantId,
            DomainId = domain.Id,
            Name = "Webhook rule",
            MatchPattern = "*@test.example.com",
            Action = InboundRuleAction.Webhook, // CLR default (0) - the regression case
            WebhookUrl = "https://example.com/hook",
        };
        ctx.InboundRules.Add(rule);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.InboundRules.AsNoTracking().SingleAsync(r => r.Id == rule.Id);
        loaded.Action.Should().Be(InboundRuleAction.Webhook);
    }

    [Fact]
    public async Task Subscription_Should_PersistExplicitProvider_EvenWhenItEqualsClrDefault()
    {
        // None is the CLR default (enum value 0). Before the fix, setting Provider = None
        // would silently be overwritten by the DB default (Stripe).
        using var ctx = CreateContext();

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = "Free",
            Tier = PlanTier.Free,
        };
        ctx.Plans.Add(plan);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Acme" };
        ctx.Tenants.Add(tenant);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.None, // CLR default (0) - the regression case
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30),
        };
        ctx.Subscriptions.Add(subscription);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Subscriptions.AsNoTracking().SingleAsync(s => s.Id == subscription.Id);
        loaded.Provider.Should().Be(PaymentProvider.None);
    }

    [Fact]
    public async Task InboundRule_Should_PersistExplicitNonDefaultAction()
    {
        // Non-regression: caller sets Forward (not CLR default, not DB default) - stored as-is.
        using var ctx = CreateContext();

        var domain = new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DomainName = "fwd.example.com",
        };
        ctx.Domains.Add(domain);

        var rule = new InboundRule
        {
            Id = Guid.NewGuid(),
            TenantId = domain.TenantId,
            DomainId = domain.Id,
            Name = "Forward rule",
            MatchPattern = "*@fwd.example.com",
            Action = InboundRuleAction.Forward,
            ForwardTo = "ops@example.com",
        };
        ctx.InboundRules.Add(rule);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.InboundRules.AsNoTracking().SingleAsync(r => r.Id == rule.Id);
        loaded.Action.Should().Be(InboundRuleAction.Forward);
    }

    [Fact]
    public async Task Subscription_Should_PersistExplicitNonDefaultProvider()
    {
        // Non-regression: caller sets PayStack - stored as-is.
        using var ctx = CreateContext();

        var plan = new Plan { Id = Guid.NewGuid(), Name = "Pro", Tier = PlanTier.Pro };
        ctx.Plans.Add(plan);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Acme2" };
        ctx.Tenants.Add(tenant);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30),
        };
        ctx.Subscriptions.Add(subscription);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Subscriptions.AsNoTracking().SingleAsync(s => s.Id == subscription.Id);
        loaded.Provider.Should().Be(PaymentProvider.PayStack);
    }
}
