using EaaS.Api.Features.Emails;
using EaaS.Api.Services;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Emails;

public sealed class ScheduleEmailHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ISubscriptionLimitService _subscriptionLimitService;
    private readonly ScheduleEmailHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ScheduleEmailHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _subscriptionLimitService = Substitute.For<ISubscriptionLimitService>();

        // Default: allow sending
        _subscriptionLimitService.CanSendEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Seed verified domain used by all tests ("example.com")
        _dbContext.Domains.Add(new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainName = "example.com",
            Status = DomainStatus.Verified,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();

        var suppressionCache = Substitute.For<ISuppressionCache>();
        suppressionCache.IsEmailSuppressedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var suppressionChecker = new SuppressionChecker(suppressionCache, _dbContext);

        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<ScheduleEmailHandler>>();
        _sut = new ScheduleEmailHandler(_dbContext, _subscriptionLimitService, suppressionChecker, logger);
    }

    [Fact]
    public async Task Should_QueueEmail_WithScheduledAt_InFuture()
    {
        var scheduledAt = DateTime.UtcNow.AddHours(2);
        var command = new ScheduleEmailCommand(
            TenantId: _tenantId,
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Future Email",
            HtmlBody: "<p>Hello from the future</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: scheduledAt);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.EmailId.Should().NotBe(Guid.Empty);
        result.ScheduledAt.Should().Be(scheduledAt);
        result.Status.Should().Be("scheduled");
    }

    [Fact]
    public async Task Should_RejectSchedule_WhenTimeInPast()
    {
        var command = new ScheduleEmailCommand(
            TenantId: _tenantId,
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Past Email",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddMinutes(-10));

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*past*");
    }

    [Fact]
    public async Task Should_RejectSchedule_WhenTimeMoreThan30DaysOut()
    {
        var command = new ScheduleEmailCommand(
            TenantId: _tenantId,
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Far Future Email",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddDays(31));

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*30 days*");
    }

    [Fact]
    public async Task Should_SetEmailStatus_ToScheduled()
    {
        var command = new ScheduleEmailCommand(
            TenantId: _tenantId,
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Scheduled Email",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = await _sut.Handle(command, CancellationToken.None);

        var savedEmail = await _dbContext.Emails.FindAsync(result.EmailId);
        savedEmail.Should().NotBeNull();
        savedEmail!.Status.Should().Be(EmailStatus.Scheduled);
        savedEmail.ScheduledAt.Should().Be(result.ScheduledAt);
    }

    [Fact]
    public async Task Should_IncludeTenantId_InMessage()
    {
        var command = new ScheduleEmailCommand(
            TenantId: _tenantId,
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Tenant Email",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = await _sut.Handle(command, CancellationToken.None);

        var savedEmail = await _dbContext.Emails.FindAsync(result.EmailId);
        savedEmail.Should().NotBeNull();
        savedEmail!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Should_RejectSchedule_WhenQuotaExceeded()
    {
        _subscriptionLimitService.CanSendEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new ScheduleEmailCommand(
            TenantId: _tenantId,
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Over Quota",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<QuotaExceededException>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
