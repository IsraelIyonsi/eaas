using System.Text.Json;
using EaaS.WebhookProcessor.Configuration;
using EaaS.WebhookProcessor.Handlers;
using EaaS.WebhookProcessor.Models;
using EaaS.WebhookProcessor.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace EaaS.WebhookProcessor.Tests;

public sealed class SnsMessageHandlerTests : IDisposable
{
    private readonly SnsTestFixture _sns = new();
    private readonly IBounceHandler _bounce = Substitute.For<IBounceHandler>();
    private readonly IComplaintHandler _complaint = Substitute.For<IComplaintHandler>();
    private readonly IDeliveryHandler _delivery = Substitute.For<IDeliveryHandler>();

    public void Dispose()
    {
        _sns.Dispose();
        GC.SuppressFinalize(this);
    }

    private SnsMessageHandler BuildHandler(HttpMessageHandler? httpHandler = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient(httpHandler ?? new ThrowingHandler()));
        return new SnsMessageHandler(
            _bounce,
            _complaint,
            _delivery,
            factory,
            _sns.Verifier,
            InMemoryRedis.Build(),
            NullLogger<SnsMessageHandler>.Instance);
    }

    private static string BounceMessageJson() => JsonSerializer.Serialize(new SesNotification
    {
        NotificationType = "Bounce",
        Mail = new SesMail { MessageId = "ses-mid-1" }
    });

    [Fact]
    public async Task ValidSignedBounceNotification_Returns200_AndInvokesHandlerOnce()
    {
        var msg = _sns.BuildSignedNotification(BounceMessageJson());
        var handler = BuildHandler();

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(200);
        await _bounce.Received(1).HandleAsync(Arg.Any<SesNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateMessageId_ReturnsOk_ButHandlerNotInvokedAgain()
    {
        // Reuse one IConnectionMultiplexer across both calls so dedup state carries over.
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new ThrowingHandler()));
        var redis = InMemoryRedis.Build();
        var handler = new SnsMessageHandler(
            _bounce, _complaint, _delivery,
            factory, _sns.Verifier, redis, NullLogger<SnsMessageHandler>.Instance);

        var msg = _sns.BuildSignedNotification(BounceMessageJson(), messageId: "fixed-mid");

        (await (await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None)).ExecuteAsync())
            .Should().Be(200);
        (await (await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None)).ExecuteAsync())
            .Should().Be(200);

        await _bounce.Received(1).HandleAsync(Arg.Any<SesNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignatureVerificationFails_Returns403_AndNoSideEffect()
    {
        var msg = _sns.BuildSignedNotification(BounceMessageJson());
        msg.Message = "tampered"; // invalidates signature
        var handler = BuildHandler();

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(403);
        await _bounce.DidNotReceive().HandleAsync(Arg.Any<SesNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MalformedJson_Returns403()
    {
        var handler = BuildHandler();

        var result = await handler.HandleAsync(FakeHttpRequest.FromBody("{not-json"), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(403);
    }

    [Fact]
    public async Task UnknownType_Returns403()
    {
        var msg = _sns.BuildSignedNotification(BounceMessageJson());
        msg.Type = "MysteryType"; // verifier will reject at canonical-string step
        var handler = BuildHandler();

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(403);
    }

    [Fact]
    public async Task RedisThrows_FailsOpen_Returns200_AndInvokesDownstreamHandler()
    {
        // Redis being unreachable must NOT 500 the webhook: signature is already authenticated and
        // downstream bounce/complaint/delivery handlers are idempotent by MessageId.
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new ThrowingHandler()));
        var redis = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>())
            .Returns<Task<bool>>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);

        var handler = new SnsMessageHandler(
            _bounce, _complaint, _delivery,
            factory, _sns.Verifier, redis, NullLogger<SnsMessageHandler>.Instance);

        var msg = _sns.BuildSignedNotification(BounceMessageJson());
        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(200);
        await _bounce.Received(1).HandleAsync(Arg.Any<SesNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignatureVerificationDisabled_AcceptsUnsignedPayload_AndInvokesDownstream()
    {
        // Kill switch on → unsigned / tampered payloads accepted, but loud error log + metric emitted.
        // Here we flag-off the verifier and tamper the signature; handler must still 200 and invoke bounce.
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new ThrowingHandler()));
        var opts = TestOptionsMonitor.Create(new SnsWebhookOptions { SignatureVerificationEnabled = false });
        var disabledVerifier = new SnsSignatureVerifier(
            factory, NullLogger<SnsSignatureVerifier>.Instance, _sns.TimeProvider, opts);

        var handler = new SnsMessageHandler(
            _bounce, _complaint, _delivery,
            factory, disabledVerifier, InMemoryRedis.Build(), NullLogger<SnsMessageHandler>.Instance);

        var msg = _sns.BuildSignedNotification(BounceMessageJson());
        msg.Signature = "!!!intentionally-invalid!!!"; // would 403 under normal flow

        var result = await handler.HandleAsync(FakeHttpRequest.FromJson(msg), CancellationToken.None);

        (await result.ExecuteAsync()).Should().Be(200);
        await _bounce.Received(1).HandleAsync(Arg.Any<SesNotification>(), Arg.Any<CancellationToken>());
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => throw new HttpRequestException("no network");
    }
}
