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
    private readonly ISubscriptionLimitService _subscriptionLimitService;
    private readonly ITemplateRenderingService _templateRenderingService;
    private readonly SendEmailHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public SendEmailHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _rateLimiter = Substitute.For<IRateLimiter>();
        _idempotencyStore = Substitute.For<IIdempotencyStore>();
        _suppressionCache = Substitute.For<ISuppressionCache>();
        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        _subscriptionLimitService = Substitute.For<ISubscriptionLimitService>();

        // Default: allow all rate limit checks
        _rateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Default: allow sending emails (within quota)
        _subscriptionLimitService.CanSendEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var suppressionChecker = new SuppressionChecker(_suppressionCache, _dbContext);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<EaaS.Api.Features.Emails.SendEmailHandler>>();
        _templateRenderingService = Substitute.For<ITemplateRenderingService>();
        _sut = new SendEmailHandler(_dbContext, _rateLimiter, _idempotencyStore, _publishEndpoint, suppressionChecker, _subscriptionLimitService, _templateRenderingService, logger);

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
        result.MessageId.Should().StartWith("snx_");

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
        var cachedMessageId = "snx_cached123";
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

    [Fact]
    public async Task Should_ThrowRateLimitExceeded_WhenRateLimitDenied()
    {
        _rateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExceededException>()
            .WithMessage("*Rate limit exceeded*");
    }

    [Fact]
    public async Task Should_ThrowQuotaExceeded_WhenSubscriptionLimitDenied()
    {
        _subscriptionLimitService.CanSendEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<QuotaExceededException>()
            .WithMessage("*email limit exceeded*");
    }

    // ---------------------------------------------------------------------
    // BUG-H1: template hydration in the handler (not the validator).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BugH1_Should_HydrateBodyFromTemplate_When_TemplateOnly()
    {
        // Arrange: template with body + subject, no inline body on the request.
        var templateId = Guid.NewGuid();
        _dbContext.Templates.Add(new Template
        {
            Id = templateId,
            TenantId = _tenantId,
            Name = "welcome",
            SubjectTemplate = "Hi {{ name }}",
            HtmlBody = "<p>Hello {{ name }}</p>",
            TextBody = "Hello {{ name }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _templateRenderingService.RenderAsync(
            "Hi {{ name }}", "<p>Hello {{ name }}</p>", "Hello {{ name }}",
            Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("Hi Ada", "<p>Hello Ada</p>", "Hello Ada"));

        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .WithSubject(null)
            .WithHtmlBody(null)
            .WithTextBody(null)
            .WithTemplateId(templateId)
            .WithVariables(new Dictionary<string, object> { ["name"] = "Ada" })
            .Build();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert: persisted Email has the rendered body — no null/"failed" downstream.
        var saved = await _dbContext.Emails.FindAsync(result.Id);
        saved!.Subject.Should().Be("Hi Ada");
        saved.HtmlBody.Should().Be("<p>Hello Ada</p>");
        saved.TextBody.Should().Be("Hello Ada");
        saved.TemplateId.Should().Be(templateId); // audit trail preserved.
    }

    [Fact]
    public async Task BugH1_Should_PreferInlineBody_When_TemplateAndInlineProvided()
    {
        var templateId = Guid.NewGuid();
        _dbContext.Templates.Add(new Template
        {
            Id = templateId,
            TenantId = _tenantId,
            Name = "welcome",
            SubjectTemplate = "Template Subject",
            HtmlBody = "<p>From template</p>",
            TextBody = "From template",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _templateRenderingService.RenderAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("Template Subject", "<p>From template</p>", "From template"));

        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .WithSubject("Inline Wins")
            .WithHtmlBody("<p>Inline HTML</p>")
            .WithTextBody("Inline text")
            .WithTemplateId(templateId)
            .Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        var saved = await _dbContext.Emails.FindAsync(result.Id);
        saved!.Subject.Should().Be("Inline Wins");
        saved.HtmlBody.Should().Be("<p>Inline HTML</p>");
        saved.TextBody.Should().Be("Inline text");
    }

    [Fact]
    public async Task BugH1_Should_Throw404_When_TemplateDoesNotExist()
    {
        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .WithHtmlBody(null)
            .WithTextBody(null)
            .WithTemplateId(Guid.NewGuid())
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Template*not found*");
    }

    [Fact]
    public async Task BugH1_Should_Throw404_When_TemplateBelongsToDifferentTenant()
    {
        // Template owned by a different tenant — must not be visible to _tenantId.
        var otherTenantId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        _dbContext.Templates.Add(new Template
        {
            Id = templateId,
            TenantId = otherTenantId,
            Name = "leaky",
            SubjectTemplate = "s",
            HtmlBody = "h",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var command = TestDataBuilders.SendEmail()
            .WithTenantId(_tenantId)
            .WithFrom("sender@verified.com")
            .WithHtmlBody(null)
            .WithTextBody(null)
            .WithTemplateId(templateId)
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        // Same 404 as missing — do NOT leak that the template exists under another tenant.
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Template*not found*");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
