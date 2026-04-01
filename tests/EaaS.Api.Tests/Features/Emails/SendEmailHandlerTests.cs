using EaaS.Domain.Exceptions;
using System.Text.Json;
using EaaS.Api.Features.Emails;
using EaaS.Api.Services;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Features.Emails;

public sealed class SendEmailHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IRateLimiter _rateLimiter;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ISuppressionCache _suppressionCache;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly SendEmailHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public SendEmailHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _rateLimiter = Substitute.For<IRateLimiter>();
        _idempotencyStore = Substitute.For<IIdempotencyStore>();
        _suppressionCache = Substitute.For<ISuppressionCache>();
        _publishEndpoint = Substitute.For<IPublishEndpoint>();

        // Default: allow all rate limit checks
        _rateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var suppressionChecker = new SuppressionChecker(_suppressionCache, _dbContext);
        _sut = new SendEmailHandler(_dbContext, _rateLimiter, _idempotencyStore, _publishEndpoint, suppressionChecker);

        // Seed a verified domain
        _dbContext.Domains.Add(new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainName = "verified.com",
            Status = DomainStatus.Verified,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Should_CreateEmailAndPublishMessage_When_ValidRequest()
    {
        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .Build();

        _suppressionCache.IsEmailSuppressedAsync(_tenantId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("queued");
        result.MessageId.Should().StartWith("eaas_");

        var savedEmail = await _dbContext.Emails.FindAsync(result.Id);
        savedEmail.Should().NotBeNull();
        savedEmail!.Status.Should().Be(EmailStatus.Queued);

        await _publishEndpoint.Received(1).Publish(
            Arg.Any<EaaS.Infrastructure.Messaging.Contracts.SendEmailMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnSuppressed_When_RecipientOnSuppressionList()
    {
        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .WithTo(new List<string> { "suppressed@example.com" })
            .Build();

        _suppressionCache.IsEmailSuppressedAsync(_tenantId, "suppressed@example.com", Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<RecipientSuppressedException>()
            .WithMessage("*suppression list*");
    }

    [Fact]
    public async Task Should_ReturnError_When_DomainNotVerified()
    {
        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@unverified.com")
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainNotVerifiedException>()
            .WithMessage("*not verified*");
    }

    [Fact]
    public async Task Should_ReturnCachedResult_When_IdempotencyKeyExists()
    {
        var cachedId = Guid.NewGuid();
        var cachedMessageId = "eaas_cached123";
        var cachedData = JsonSerializer.Serialize(new { Id = cachedId, MessageId = cachedMessageId });

        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .WithIdempotencyKey("unique-key-123")
            .Build();

        _idempotencyStore.GetIdempotencyKeyAsync(_tenantId, "unique-key-123", Arg.Any<CancellationToken>())
            .Returns(cachedData);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Id.Should().Be(cachedId);
        result.MessageId.Should().Be(cachedMessageId);
        result.Status.Should().Be("queued");

        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<EaaS.Infrastructure.Messaging.Contracts.SendEmailMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_StoreIdempotencyKey_When_Provided()
    {
        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .WithIdempotencyKey("new-key-456")
            .Build();

        _suppressionCache.IsEmailSuppressedAsync(_tenantId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _idempotencyStore.GetIdempotencyKeyAsync(_tenantId, "new-key-456", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await _sut.Handle(command, CancellationToken.None);

        await _idempotencyStore.Received(1).SetIdempotencyKeyAsync(
            _tenantId,
            "new-key-456",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
