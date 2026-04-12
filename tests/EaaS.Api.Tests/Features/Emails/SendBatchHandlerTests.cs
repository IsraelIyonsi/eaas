using EaaS.Api.Features.Emails;
using EaaS.Api.Services;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Exceptions;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Emails;

public sealed class SendBatchHandlerTests
{
    private readonly IRateLimiter _rateLimiter = Substitute.For<IRateLimiter>();
    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly ISuppressionCache _suppressionCache = Substitute.For<ISuppressionCache>();
    private readonly ISubscriptionLimitService _subscriptionLimitService = Substitute.For<ISubscriptionLimitService>();
    private readonly ILogger<SendBatchHandler> _logger = Substitute.For<ILogger<SendBatchHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _apiKeyId = Guid.NewGuid();

    public SendBatchHandlerTests()
    {
        _rateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _suppressionCache.IsEmailSuppressedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Default: quota allows sending
        _subscriptionLimitService.CanSendEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task Should_SendAllEmails_WhenAllValid()
    {
        var (dbContext, sut) = CreateSut();
        SeedVerifiedDomain(dbContext, _tenantId, "verified.com");

        var batchEmails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "to1@example.com" }, null, null, "Subject 1", "<p>Body1</p>", "Body1", null, null, null, null),
            new("sender@verified.com", new List<string> { "to2@example.com" }, null, null, "Subject 2", "<p>Body2</p>", "Body2", null, null, null, null),
        };

        var command = new SendBatchCommand(_tenantId, _apiKeyId, batchEmails);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Total.Should().Be(2);
        result.Accepted.Should().Be(2);
        result.Rejected.Should().Be(0);
        result.BatchId.Should().NotBeNullOrEmpty();
        result.Messages.Should().HaveCount(2);
        result.Messages.Should().AllSatisfy(m => m.Status.Should().Be("queued"));
    }

    [Fact]
    public async Task Should_ReturnPartialSuccess_WhenSomeEmailsInvalid()
    {
        var (dbContext, sut) = CreateSut();
        SeedVerifiedDomain(dbContext, _tenantId, "verified.com");

        var batchEmails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "to1@example.com" }, null, null, "Subject", "<p>Body</p>", "Body", null, null, null, null),
            new("sender@unverified.com", new List<string> { "to2@example.com" }, null, null, "Subject", "<p>Body</p>", "Body", null, null, null, null),
        };

        var command = new SendBatchCommand(_tenantId, _apiKeyId, batchEmails);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Total.Should().Be(2);
        result.Accepted.Should().Be(1);
        result.Rejected.Should().Be(1);
        result.Messages[0].Status.Should().Be("queued");
        result.Messages[1].Status.Should().Be("rejected");
        result.Messages[1].Error.Should().Contain("not verified");
    }

    [Fact]
    public async Task Should_CheckSuppression_ForEachRecipient()
    {
        _suppressionCache.IsEmailSuppressedAsync(Arg.Any<Guid>(), Arg.Is("suppressed@example.com"), Arg.Any<CancellationToken>())
            .Returns(true);

        var (dbContext, sut) = CreateSut();
        SeedVerifiedDomain(dbContext, _tenantId, "verified.com");

        var batchEmails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "suppressed@example.com" }, null, null, "Subject", "<p>Body</p>", "Body", null, null, null, null),
        };

        var command = new SendBatchCommand(_tenantId, _apiKeyId, batchEmails);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Rejected.Should().Be(1);
        result.Messages[0].Status.Should().Be("rejected");
        result.Messages[0].Error.Should().Contain("suppression");
    }

    [Fact]
    public async Task Should_EnqueueToMassTransit()
    {
        var (dbContext, sut) = CreateSut();
        SeedVerifiedDomain(dbContext, _tenantId, "verified.com");

        var batchEmails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "to@example.com" }, null, null, "Subject", "<p>Body</p>", "Body", null, null, null, null),
        };

        var command = new SendBatchCommand(_tenantId, _apiKeyId, batchEmails);

        await sut.Handle(command, CancellationToken.None);

        await _publishEndpoint.Received(1).Publish(
            Arg.Any<EaaS.Infrastructure.Messaging.Contracts.SendEmailMessage>(),
            Arg.Any<CancellationToken>());
    }

    private (AppDbContext dbContext, SendBatchHandler handler) CreateSut()
    {
        var dbContext = DbContextFactory.Create();
        var suppressionChecker = new SuppressionChecker(_suppressionCache, dbContext);
        var handler = new SendBatchHandler(dbContext, _rateLimiter, _publishEndpoint, suppressionChecker, _subscriptionLimitService, _logger);
        return (dbContext, handler);
    }

    [Fact]
    public async Task Should_ThrowRateLimitExceeded_WhenRateLimitDenied()
    {
        _rateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var (dbContext, sut) = CreateSut();
        SeedVerifiedDomain(dbContext, _tenantId, "verified.com");

        var batchEmails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "to@example.com" }, null, null, "Subject", "<p>Body</p>", "Body", null, null, null, null),
        };

        var command = new SendBatchCommand(_tenantId, _apiKeyId, batchEmails);

        var act = () => sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExceededException>()
            .WithMessage("*Rate limit exceeded*");
    }

    [Fact]
    public async Task Should_PublishMessages_OnlyAfterSaveChanges()
    {
        var (dbContext, sut) = CreateSut();
        SeedVerifiedDomain(dbContext, _tenantId, "verified.com");

        var publishCallOrder = new List<string>();

        // Track when SaveChanges is called by intercepting the publish endpoint
        // If publish is called before SaveChanges, the email won't exist in the DB yet
        _publishEndpoint.Publish(
            Arg.Any<EaaS.Infrastructure.Messaging.Contracts.SendEmailMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // At the time of publish, verify the email has been saved to the DB
                var emailCount = dbContext.Emails.Count();
                emailCount.Should().BeGreaterThan(0, "emails must be saved to DB before publishing messages");
                return Task.CompletedTask;
            });

        var batchEmails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "to@example.com" }, null, null, "Subject", "<p>Body</p>", "Body", null, null, null, null),
        };

        var command = new SendBatchCommand(_tenantId, _apiKeyId, batchEmails);

        await sut.Handle(command, CancellationToken.None);

        // Verify publish was actually called
        await _publishEndpoint.Received(1).Publish(
            Arg.Any<EaaS.Infrastructure.Messaging.Contracts.SendEmailMessage>(),
            Arg.Any<CancellationToken>());
    }

    private static void SeedVerifiedDomain(AppDbContext dbContext, Guid tenantId, string domainName)
    {
        dbContext.Domains.Add(new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainName = domainName,
            Status = DomainStatus.Verified,
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }
}
