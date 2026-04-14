using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Messaging;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using Xunit;

namespace EaaS.Worker.Tests.Consumers;

public sealed class ListUnsubscribeHeaderInjectionTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly SendEmailConsumer _sut;

    public ListUnsubscribeHeaderInjectionTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _emailSender = Substitute.For<IEmailSender>();
        _emailSender.SendRawEmailAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new SendEmailResult(true, "ses-raw-1", null));

        var unsubSettings = Options.Create(new ListUnsubscribeSettings
        {
            HmacSecret = "test-secret",
            BaseUrl = "https://sendnex.xyz",
            MailtoHost = "sendnex.xyz"
        });
        var unsubService = new ListUnsubscribeService(unsubSettings);
        var footerInjector = new EmailFooterInjector();

        _sut = new SendEmailConsumer(
            _dbContext,
            _emailSender,
            Substitute.For<ITemplateRenderingService>(),
            Substitute.For<ILogger<SendEmailConsumer>>(),
            pixelInjector: null,
            linkRewriter: null,
            unsubscribeService: unsubService,
            footerInjector: footerInjector);
    }

    [Fact]
    public async Task Should_InjectListUnsubscribeHeaders_AndFooter()
    {
        var tenant = SeedTenant("Acme Legal Ltd.", "42 Marina Rd\nLagos, Nigeria");
        var email = SeedEmail(tenant.Id);
        var message = CreateMessage(email);
        var context = CreateContext(message);

        Stream? captured = null;
        _emailSender
            .When(x => x.SendRawEmailAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var src = call.Arg<Stream>();
                var copy = new MemoryStream();
                src.Position = 0;
                src.CopyTo(copy);
                copy.Position = 0;
                captured = copy;
            });

        await _sut.Consume(context);

        captured.Should().NotBeNull();
        captured!.Position = 0;
        var mime = MimeMessage.Load(captured);

        var lu = mime.Headers["List-Unsubscribe"];
        lu.Should().NotBeNull();
        lu.Should().Contain("mailto:unsubscribe+");
        lu.Should().Contain("@sendnex.xyz");
        lu.Should().Contain("https://sendnex.xyz/u/");

        mime.Headers["List-Unsubscribe-Post"].Should().Be("List-Unsubscribe=One-Click");

        var bodyText = mime.HtmlBody ?? mime.TextBody ?? string.Empty;
        bodyText.Should().Contain("Unsubscribe");
        bodyText.Should().Contain("Acme Legal Ltd.");
        bodyText.Should().Contain("42 Marina Rd");
    }

    [Fact]
    public async Task Should_UseSendRaw_When_UnsubscribeServiceAvailable()
    {
        var tenant = SeedTenant("Test Ltd.", "1 Addr");
        var email = SeedEmail(tenant.Id);
        await _sut.Consume(CreateContext(CreateMessage(email)));

        await _emailSender.Received(1).SendRawEmailAsync(
            Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_IncludeHttpsUnsubscribeLink_InFooter()
    {
        var tenant = SeedTenant("Foo Ltd.", "99 Baz");
        var email = SeedEmail(tenant.Id);
        Stream? captured = null;
        _emailSender
            .When(x => x.SendRawEmailAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var src = call.Arg<Stream>();
                var copy = new MemoryStream();
                src.Position = 0;
                src.CopyTo(copy);
                copy.Position = 0;
                captured = copy;
            });

        await _sut.Consume(CreateContext(CreateMessage(email)));

        captured!.Position = 0;
        var mime = MimeMessage.Load(captured);
        var html = mime.HtmlBody ?? string.Empty;

        html.Should().Contain("https://sendnex.xyz/u/");
    }

    private Tenant SeedTenant(string legalEntity, string postalAddress)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Tenant",
            LegalEntityName = legalEntity,
            PostalAddress = postalAddress,
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        _dbContext.SaveChanges();
        return tenant;
    }

    private Email SeedEmail(Guid tenantId)
    {
        var email = new Email
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApiKeyId = Guid.NewGuid(),
            MessageId = $"eaas_{Guid.NewGuid():N}",
            FromEmail = "sender@verified.com",
            ToEmails = "[\"recipient@example.com\"]",
            Subject = "Hello",
            HtmlBody = "<html><body><p>Hi there</p></body></html>",
            TextBody = "Hi there",
            Status = EmailStatus.Queued,
            Metadata = "{}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Emails.Add(email);
        _dbContext.SaveChanges();
        return email;
    }

    private static SendEmailMessage CreateMessage(Email email) => new()
    {
        EmailId = email.Id,
        TenantId = email.TenantId,
        From = email.FromEmail,
        To = email.ToEmails,
        Subject = email.Subject,
        HtmlBody = email.HtmlBody,
        TextBody = email.TextBody,
        Tags = Array.Empty<string>(),
        Metadata = "{}"
    };

    private static ConsumeContext<SendEmailMessage> CreateContext(SendEmailMessage message)
    {
        var context = Substitute.For<ConsumeContext<SendEmailMessage>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    public void Dispose() => _dbContext.Dispose();
}
