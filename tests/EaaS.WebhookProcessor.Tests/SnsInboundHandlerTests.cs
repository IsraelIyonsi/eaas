using System.Net;
using System.Text.Json;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.WebhookProcessor.Handlers;
using EaaS.WebhookProcessor.Models;
using EaaS.WebhookProcessor.Tests.TestSupport;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EaaS.WebhookProcessor.Tests;

public sealed class SnsInboundHandlerTests : IDisposable
{
    private readonly SnsTestFixture _sns = new();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    public void Dispose()
    {
        _sns.Dispose();
        GC.SuppressFinalize(this);
    }

    private SnsInboundHandler BuildHandler(HttpMessageHandler? httpHandler = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient(httpHandler ?? new ThrowingHandler()));
        return new SnsInboundHandler(
            _publish, factory, _sns.Verifier, InMemoryRedis.Build(),
            NullLogger<SnsInboundHandler>.Instance);
    }

    private static string InboundMessageJson() => JsonSerializer.Serialize(new SesInboundNotification
    {
        NotificationType = "Received",
        Mail = new SesInboundMail
        {
            MessageId = "ses-inbound-1",
            Destination = new List<string> { "inbox@example.com" }
        },
        Receipt = new SesInboundReceipt
        {
            Action = new SesInboundAction { BucketName = "bkt", ObjectKey = "key" },
            SpamVerdict = new SesVerdict { Status = "PASS" },
            VirusVerdict = new SesVerdict { Status = "PASS" },
            SpfVerdict = new SesVerdict { Status = "PASS" },
            DkimVerdict = new SesVerdict { Status = "PASS" },
            DmarcVerdict = new SesVerdict { Status = "PASS" }
        }
    });

    [Fact]
    public async Task ValidSignedInbound_Returns200_AndPublishesOnce()
    {
        var msg = _sns.BuildSignedNotification(InboundMessageJson());
        var handler = BuildHandler();

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(200);
        await _publish.Received(1).Publish(Arg.Any<ProcessInboundEmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateMessageId_ReturnsOk_NoSecondPublish()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new ThrowingHandler()));
        var redis = InMemoryRedis.Build();
        var handler = new SnsInboundHandler(
            _publish, factory, _sns.Verifier, redis,
            NullLogger<SnsInboundHandler>.Instance);

        var msg = _sns.BuildSignedNotification(InboundMessageJson(), messageId: "fixed-inbound");

        (await (await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None)).ExecuteAsync())
            .Should().Be(200);
        (await (await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None)).ExecuteAsync())
            .Should().Be(200);

        await _publish.Received(1).Publish(Arg.Any<ProcessInboundEmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignatureFails_Returns403_NoPublish()
    {
        var msg = _sns.BuildSignedNotification(InboundMessageJson());
        msg.Message = "tampered";
        var handler = BuildHandler();

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(403);
        await _publish.DidNotReceive().Publish(Arg.Any<ProcessInboundEmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MalformedJson_Returns403()
    {
        var handler = BuildHandler();
        var result = await handler.HandleAsync(FakeHttpRequest.FromBody("not-json"), CancellationToken.None);
        (await result.ExecuteAsync()).Should().Be(403);
    }

    [Fact]
    public async Task UnknownType_Returns403()
    {
        var msg = _sns.BuildSignedNotification(InboundMessageJson());
        msg.Type = "Weird";
        var handler = BuildHandler();
        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);
        (await result.ExecuteAsync()).Should().Be(403);
    }

    [Fact]
    public async Task SubscriptionConfirmation_WhenSubscribeUrlFetchThrows_Returns502()
    {
        var msg = _sns.BuildSignedSubscriptionConfirmation();
        var handler = BuildHandler(new ThrowingHandler());

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(502);
    }

    [Fact]
    public async Task SubscriptionConfirmation_WhenSubscribeUrlReturns500_Returns502()
    {
        var msg = _sns.BuildSignedSubscriptionConfirmation();
        var handler = BuildHandler(new StubHandler(HttpStatusCode.InternalServerError));

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(502);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => throw new HttpRequestException("SSRF guard blocked / network down");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public StubHandler(HttpStatusCode status) => _status = status;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_status));
    }
}
