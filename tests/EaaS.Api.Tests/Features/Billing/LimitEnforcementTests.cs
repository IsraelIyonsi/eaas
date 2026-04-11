using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing;

public sealed class LimitEnforcementTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly SubscriptionLimitService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public LimitEnforcementTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new SubscriptionLimitService(_dbContext);
    }

    [Fact]
    public async Task Should_AllowSendEmail_WhenUnderMonthlyLimit()
    {
        // Arrange: tenant with a Pro plan (50,000/month), only 10 emails sent this month
        var plan = SeedPlan(PlanTier.Pro, monthlyEmailLimit: 50_000, dailyEmailLimit: 5000);
        SeedActiveSubscription(_tenantId, plan.Id);
        SeedEmailsThisMonth(_tenantId, count: 10);

        // Act
        var canSend = await _sut.CanSendEmailAsync(_tenantId);

        // Assert
        canSend.Should().BeTrue();
    }

    [Fact]
    public async Task Should_RejectSendEmail_WhenMonthlyLimitExceeded()
    {
        // Arrange: tenant with a Starter plan (10,000/month), already at limit
        var plan = SeedPlan(PlanTier.Starter, monthlyEmailLimit: 10_000, dailyEmailLimit: 1000);
        SeedActiveSubscription(_tenantId, plan.Id);
        SeedEmailsThisMonth(_tenantId, count: 10_000);

        // Act
        var canSend = await _sut.CanSendEmailAsync(_tenantId);

        // Assert
        canSend.Should().BeFalse();
    }

    [Fact]
    public async Task Should_UsePlanLimits_WhenSubscriptionActive()
    {
        // Arrange: tenant with an active Pro plan
        var plan = SeedPlan(PlanTier.Pro, monthlyEmailLimit: 50_000, dailyEmailLimit: 5000,
            maxApiKeys: 10, maxDomains: 5, maxTemplates: 50, maxWebhooks: 20);
        SeedActiveSubscription(_tenantId, plan.Id);

        // Act
        var limits = await _sut.GetLimitsAsync(_tenantId);

        // Assert
        limits.MonthlyEmailLimit.Should().Be(50_000);
        limits.DailyEmailLimit.Should().Be(5000);
        limits.MaxApiKeys.Should().Be(10);
        limits.MaxDomains.Should().Be(5);
        limits.MaxTemplates.Should().Be(50);
        limits.MaxWebhooks.Should().Be(20);
    }

    [Fact]
    public async Task Should_UseFreeDefaults_WhenNoSubscription()
    {
        // Arrange: tenant with no subscription at all

        // Act
        var limits = await _sut.GetLimitsAsync(_tenantId);

        // Assert
        limits.DailyEmailLimit.Should().Be(100);
        limits.MonthlyEmailLimit.Should().Be(3000);
        limits.MaxApiKeys.Should().Be(3);
        limits.MaxDomains.Should().Be(2);
        limits.MaxTemplates.Should().Be(10);
        limits.MaxWebhooks.Should().Be(5);
    }

    [Fact]
    public async Task Should_AllowSendEmail_WhenFreePlan_UnderLimit()
    {
        // Arrange: no subscription (free defaults = 3000/month), 100 emails sent
        SeedEmailsThisMonth(_tenantId, count: 100);

        // Act
        var canSend = await _sut.CanSendEmailAsync(_tenantId);

        // Assert
        canSend.Should().BeTrue();
    }

    [Fact]
    public async Task Should_RejectCreateApiKey_WhenMaxApiKeysReached()
    {
        // Arrange: free defaults = 3 API keys, already have 3
        SeedApiKeys(_tenantId, count: 3);

        // Act
        var canCreate = await _sut.CanCreateApiKeyAsync(_tenantId);

        // Assert
        canCreate.Should().BeFalse();
    }

    [Fact]
    public async Task Should_RejectAddDomain_WhenMaxDomainsReached()
    {
        // Arrange: free defaults = 2 domains, already have 2
        SeedDomains(_tenantId, count: 2);

        // Act
        var canAdd = await _sut.CanAddDomainAsync(_tenantId);

        // Assert
        canAdd.Should().BeFalse();
    }

    [Fact]
    public async Task Should_AllowOperations_WhenEnterprisePlan()
    {
        // Arrange: enterprise plan with very high limits
        var plan = SeedPlan(PlanTier.Enterprise, monthlyEmailLimit: 10_000_000, dailyEmailLimit: 500_000,
            maxApiKeys: 100, maxDomains: 50, maxTemplates: 500, maxWebhooks: 100);
        SeedActiveSubscription(_tenantId, plan.Id);
        SeedEmailsThisMonth(_tenantId, count: 50_000);
        SeedApiKeys(_tenantId, count: 20);
        SeedDomains(_tenantId, count: 10);

        // Act & Assert
        (await _sut.CanSendEmailAsync(_tenantId)).Should().BeTrue();
        (await _sut.CanCreateApiKeyAsync(_tenantId)).Should().BeTrue();
        (await _sut.CanAddDomainAsync(_tenantId)).Should().BeTrue();
    }

    // --- Helpers ---

    private Plan SeedPlan(PlanTier tier, long monthlyEmailLimit, int dailyEmailLimit,
        int maxApiKeys = 10, int maxDomains = 5, int maxTemplates = 50, int maxWebhooks = 20)
    {
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = tier.ToString(),
            Tier = tier,
            MonthlyPriceUsd = tier == PlanTier.Free ? 0m : 29.99m,
            AnnualPriceUsd = tier == PlanTier.Free ? 0m : 299.99m,
            DailyEmailLimit = dailyEmailLimit,
            MonthlyEmailLimit = monthlyEmailLimit,
            MaxApiKeys = maxApiKeys,
            MaxDomains = maxDomains,
            MaxTemplates = maxTemplates,
            MaxWebhooks = maxWebhooks,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Plans.Add(plan);
        _dbContext.SaveChanges();
        return plan;
    }

    private void SeedActiveSubscription(Guid tenantId, Guid planId)
    {
        _dbContext.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            Provider = PaymentProvider.PayStack,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    private void SeedEmailsThisMonth(Guid tenantId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _dbContext.Emails.Add(new Email
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ApiKeyId = Guid.NewGuid(),
                MessageId = $"eaas_{Guid.NewGuid():N}",
                FromEmail = "sender@test.com",
                ToEmails = "[\"to@test.com\"]",
                Subject = "Test",
                Status = EmailStatus.Queued,
                Metadata = "{}",
                CreatedAt = DateTime.UtcNow
            });
        }
        _dbContext.SaveChanges();
    }

    private void SeedApiKeys(Guid tenantId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _dbContext.ApiKeys.Add(new ApiKey
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Key {i}",
                KeyHash = $"hash_{i}",
                Prefix = $"eaas_{i:D4}",
                Status = ApiKeyStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        }
        _dbContext.SaveChanges();
    }

    private void SeedDomains(Guid tenantId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _dbContext.Domains.Add(new SendingDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DomainName = $"domain{i}.com",
                Status = DomainStatus.Verified,
                CreatedAt = DateTime.UtcNow
            });
        }
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
