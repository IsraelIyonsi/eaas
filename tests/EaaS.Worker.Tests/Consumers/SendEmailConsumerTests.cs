using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Domain.Providers;
using EaaS.Infrastructure.Messaging;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EaaS.Worker.Tests.Consumers;

public sealed class SendEmailConsumerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailProvider _provider;
    private readonly IEmailProviderFactory _providerFactory;
    private readonly ITemplateRenderingService _templateRenderingService;
    private readonly ILogger<SendEmailConsumer> _logger;
    private readonly SendEmailConsumer _sut;

    public SendEmailConsumerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _provider = Substitute.For<IEmailProvider>();
        _provider.ProviderKey.Returns("ses");
        _provider.Capabilities.Returns(EmailProviderCapability.SendRaw | EmailProviderCapability.Attachments);

        _providerFactory = Substitute.For<IEmailProviderFactory>();
        _providerFactory.GetForTenant(Arg.Any<Guid>()).Returns(_provider);

        _templateRenderingService = Substitute.For<ITemplateRenderingService>();
        _logger = Substitute.For<ILogger<SendEmailConsumer>>();

        _sut = new SendEmailConsumer(
            _dbContext, _providerFactory, _templateRenderingService, _logger);
    }

    [Fact]
    public async Task Should_SendViaProvider_When_MessageReceived()
    {
        var email = SeedEmail();
        var message = CreateMessage(email);
        var context = CreateConsumeContext(message);

        _provider.SendAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendOutcome(true, "ses-msg-123", null, null, false));

        await _sut.Consume(context);

        await _provider.Received(1).SendAsync(
            Arg.Is<SendEmailRequest>(r =>
                r.From == email.FromEmail
                && r.Subject == email.Subject
                && r.HtmlBody == email.HtmlBody
                && r.TextBody == email.TextBody),
            Arg.Any<CancellationToken>());

        var updated = await _dbContext.Emails.FindAsync(email.Id);
        updated!.Status.Should().Be(EmailStatus.Sent);
        updated.SesMessageId.Should().Be("ses-msg-123");
        updated.ProviderMessageId.Should().Be("ses-msg-123");
        updated.ProviderKey.Should().Be("ses");
    }

    [Fact]
    public async Task Should_RenderTemplate_When_TemplateIdProvided()
    {
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            TenantId = Guid.NewGuid(),
            Name = "Welcome",
            SubjectTemplate = "Hello {{ name }}",
            HtmlBody = "<h1>{{ name }}</h1>",
            TextBody = "Hi {{ name }}",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Templates.Add(template);

        var email = SeedEmail(templateId: templateId);
        var message = CreateMessage(email, templateId: templateId, variables: "{\"name\":\"World\"}");
        var context = CreateConsumeContext(message);

        _templateRenderingService.RenderAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("Hello World", "<h1>World</h1>", "Hi World"));

        _provider.SendAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendOutcome(true, "ses-rendered-123", null, null, false));

        await _sut.Consume(context);

        await _templateRenderingService.Received(1).RenderAsync(
            template.SubjectTemplate,
            template.HtmlBody,
            template.TextBody,
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());

        // Provider should receive the rendered content
        await _provider.Received(1).SendAsync(
            Arg.Is<SendEmailRequest>(r =>
                r.Subject == "Hello World"
                && r.HtmlBody == "<h1>World</h1>"
                && r.TextBody == "Hi World"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_UpdateStatusToSent_When_ProviderSucceeds()
    {
        var email = SeedEmail();
        var message = CreateMessage(email);
        var context = CreateConsumeContext(message);

        _provider.SendAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendOutcome(true, "ses-delivered-123", null, null, false));

        await _sut.Consume(context);

        var updated = await _dbContext.Emails.FindAsync(email.Id);
        updated!.Status.Should().Be(EmailStatus.Sent);
        updated.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ThrowForRetry_When_ProviderFails()
    {
        var email = SeedEmail();
        var message = CreateMessage(email);
        var context = CreateConsumeContext(message);

        _provider.SendAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendOutcome(false, null, "Throttled", "SES rate limit exceeded", true));

        var act = () => _sut.Consume(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SES delivery failed*");

        var updated = await _dbContext.Emails.FindAsync(email.Id);
        updated!.Status.Should().Be(EmailStatus.Failed);
    }

    [Fact]
    public async Task Should_UpdateStatusToFailed_When_MaxRetriesExceeded()
    {
        var email = SeedEmail();
        var message = CreateMessage(email);
        var context = CreateConsumeContext(message);

        _provider.SendAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Connection timeout"));

        var act = () => _sut.Consume(context);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Connection timeout");

        var updated = await _dbContext.Emails.FindAsync(email.Id);
        updated!.Status.Should().Be(EmailStatus.Failed);
        updated.ErrorMessage.Should().Contain("Connection timeout");
    }

    private Email SeedEmail(Guid? templateId = null)
    {
        var email = new Email
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApiKeyId = Guid.NewGuid(),
            MessageId = $"eaas_{Guid.NewGuid():N}",
            FromEmail = "sender@verified.com",
            ToEmails = "[\"recipient@example.com\"]",
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            TextBody = "Hello",
            TemplateId = templateId,
            Status = EmailStatus.Queued,
            Metadata = "{}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Emails.Add(email);
        _dbContext.SaveChanges();
        return email;
    }

    private static SendEmailMessage CreateMessage(
        Email email,
        Guid? templateId = null,
        string? variables = null)
    {
        return new SendEmailMessage
        {
            EmailId = email.Id,
            TenantId = email.TenantId,
            From = email.FromEmail,
            To = email.ToEmails,
            Subject = email.Subject,
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody,
            TemplateId = templateId,
            Variables = variables,
            Tags = Array.Empty<string>(),
            Metadata = email.Metadata
        };
    }

    private static ConsumeContext<SendEmailMessage> CreateConsumeContext(SendEmailMessage message)
    {
        var context = Substitute.For<ConsumeContext<SendEmailMessage>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
