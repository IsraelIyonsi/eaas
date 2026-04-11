using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Messaging;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Worker.Tests.Consumers;

public sealed class InboundEmailConsumerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IInboundEmailStorage _storage;
    private readonly IInboundEmailParser _parser;
    private readonly ILogger<InboundEmailConsumer> _logger;
    private readonly InboundEmailConsumer _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public InboundEmailConsumerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _storage = Substitute.For<IInboundEmailStorage>();
        _parser = Substitute.For<IInboundEmailParser>();
        _logger = Substitute.For<ILogger<InboundEmailConsumer>>();

        _sut = new InboundEmailConsumer(_dbContext, _storage, _parser, _logger);
    }

    [Fact]
    public async Task Should_ProcessInboundEmail_Successfully()
    {
        // Arrange: seed a verified domain so tenant resolution works
        var domain = SeedVerifiedDomain("test.com");

        var message = new ProcessInboundEmailMessage
        {
            S3BucketName = "eaas-inbound",
            S3ObjectKey = "incoming/abc123",
            SesMessageId = "ses-msg-001",
            Recipients = new[] { "support@test.com" },
            SpamVerdict = "PASS",
            VirusVerdict = "PASS",
            SpfVerdict = "PASS",
            DkimVerdict = "PASS",
            DmarcVerdict = "PASS"
        };

        var rawStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("raw email"));
        _storage.GetRawEmailAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(rawStream);

        _storage.StoreRawEmailAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("tenant/inbound/id/raw.eml");

        _parser.Parse(Arg.Any<Stream>()).Returns(new InboundParsedEmail
        {
            FromEmail = "sender@external.com",
            FromName = "External Sender",
            ToAddresses = new[] { new EmailAddress("support@test.com", null) },
            CcAddresses = Array.Empty<EmailAddress>(),
            BccAddresses = Array.Empty<EmailAddress>(),
            Subject = "Help Request",
            HtmlBody = "<p>I need help</p>",
            TextBody = "I need help",
            Headers = new Dictionary<string, string>
            {
                ["Message-ID"] = "<msg-123@external.com>"
            },
            Attachments = Array.Empty<ParsedAttachment>()
        });

        var context = CreateConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert
        var stored = await _dbContext.InboundEmails.FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.TenantId.Should().Be(_tenantId);
        stored.FromEmail.Should().Be("sender@external.com");
        stored.Subject.Should().Be("Help Request");
        stored.Status.Should().Be(InboundEmailStatus.Processed);
        stored.ProcessedAt.Should().NotBeNull();

        stored.S3Key.Should().Be("incoming/abc123");
    }

    [Fact]
    public async Task Should_MatchReply_WhenInReplyToMatchesOutbound()
    {
        var domain = SeedVerifiedDomain("test.com");
        var outboundMessageId = "<outbound-msg-001@test.com>";

        // Seed an outbound email with a matching message ID
        var outboundEmail = new Email
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ApiKeyId = Guid.NewGuid(),
            MessageId = outboundMessageId,
            FromEmail = "support@test.com",
            ToEmails = "[\"customer@external.com\"]",
            Subject = "Original Message",
            HtmlBody = "<p>Original</p>",
            Status = EmailStatus.Delivered,
            Metadata = "{}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Emails.Add(outboundEmail);
        await _dbContext.SaveChangesAsync();

        var message = new ProcessInboundEmailMessage
        {
            S3BucketName = "eaas-inbound",
            S3ObjectKey = "incoming/reply123",
            SesMessageId = "ses-reply-001",
            Recipients = new[] { "support@test.com" },
            SpamVerdict = "PASS"
        };

        var rawStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("raw reply"));
        _storage.GetRawEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(rawStream);
        _storage.StoreRawEmailAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("tenant/inbound/id/raw.eml");

        _parser.Parse(Arg.Any<Stream>()).Returns(new InboundParsedEmail
        {
            FromEmail = "customer@external.com",
            ToAddresses = new[] { new EmailAddress("support@test.com", null) },
            CcAddresses = Array.Empty<EmailAddress>(),
            BccAddresses = Array.Empty<EmailAddress>(),
            Subject = "Re: Original Message",
            TextBody = "This is a reply",
            InReplyTo = outboundMessageId,
            Headers = new Dictionary<string, string>
            {
                ["Message-ID"] = "<reply-msg-001@external.com>",
                ["In-Reply-To"] = outboundMessageId
            },
            Attachments = Array.Empty<ParsedAttachment>()
        });

        var context = CreateConsumeContext(message);

        await _sut.Consume(context);

        var inbound = await _dbContext.InboundEmails.FirstOrDefaultAsync();
        inbound.Should().NotBeNull();
        inbound!.OutboundEmailId.Should().Be(outboundEmail.Id);
        inbound.InReplyTo.Should().Be(outboundMessageId);
    }

    [Fact]
    public async Task Should_ReturnNull_WhenNoTenantMatches()
    {
        // No domains seeded, so tenant resolution will fail
        var message = new ProcessInboundEmailMessage
        {
            S3BucketName = "eaas-inbound",
            S3ObjectKey = "incoming/unknown",
            SesMessageId = "ses-unknown-001",
            Recipients = new[] { "nobody@unregistered-domain.com" }
        };

        var context = CreateConsumeContext(message);

        await _sut.Consume(context);

        // Should gracefully return without creating any inbound email
        var count = await _dbContext.InboundEmails.CountAsync();
        count.Should().Be(0);
    }

    private SendingDomain SeedVerifiedDomain(string domainName)
    {
        var domain = new SendingDomain
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DomainName = domainName,
            Status = DomainStatus.Verified,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Domains.Add(domain);
        _dbContext.SaveChanges();
        return domain;
    }

    private static ConsumeContext<ProcessInboundEmailMessage> CreateConsumeContext(ProcessInboundEmailMessage message)
    {
        var context = Substitute.For<ConsumeContext<ProcessInboundEmailMessage>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
