using System.Net;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Messaging;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Infrastructure.Tests.Messaging;

/// <summary>
/// Regression tests for Tranche 2 G5 / H11 — <see cref="WebhookDispatchConsumer"/>
/// must dedup on <c>(webhook_id, email_id, event_type)</c> so MassTransit retries
/// never re-hit a customer endpoint that already returned a success response.
/// </summary>
public sealed class WebhookDispatchConsumerIdempotencyTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CountingHandler _httpHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebhookDispatchConsumer _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid EmailId = Guid.NewGuid();
    private const string EventType = "email.delivered";

    public WebhookDispatchConsumerIdempotencyTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AppDbContext(options);

        _httpHandler = new CountingHandler();
        var client = new HttpClient(_httpHandler);
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(_httpHandler));

        _sut = new WebhookDispatchConsumer(
            _dbContext,
            _httpClientFactory,
            Substitute.For<ILogger<WebhookDispatchConsumer>>());
    }

    [Fact]
    public async Task Consume_FirstDispatch_CreatesSucceededRowAndCallsEndpoint()
    {
        var webhook = await SeedWebhook("https://wh.example.com/a");
        _httpHandler.NextResponse = () => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.Consume(BuildContext());

        _httpHandler.CallCount.Should().Be(1);
        var row = await _dbContext.WebhookDeliveries.SingleAsync();
        row.WebhookId.Should().Be(webhook.Id);
        row.EmailId.Should().Be(EmailId);
        row.EventType.Should().Be(EventType);
        row.Status.Should().Be(WebhookDeliveryStatus.Succeeded);
        row.AttemptCount.Should().Be(1);
        row.ResponseStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Consume_SecondDispatchAfterSuccess_ShortCircuitsWithNoHttpCall()
    {
        await SeedWebhook("https://wh.example.com/a");
        _httpHandler.NextResponse = () => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.Consume(BuildContext());
        _httpHandler.CallCount.Should().Be(1);

        // Replay — same (webhook, email, event_type) tuple.
        await _sut.Consume(BuildContext());

        _httpHandler.CallCount.Should().Be(1, "replay must not hit the customer endpoint");
        var row = await _dbContext.WebhookDeliveries.SingleAsync();
        row.Status.Should().Be(WebhookDeliveryStatus.Succeeded);
        row.AttemptCount.Should().Be(1, "short-circuit path must not bump the counter");
    }

    [Fact]
    public async Task Consume_FailedDelivery_LeavesFailedRowAndRetryAttempsAgain()
    {
        await SeedWebhook("https://wh.example.com/a");
        _httpHandler.NextResponse = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Consume(BuildContext()));

        var row = await _dbContext.WebhookDeliveries.SingleAsync();
        row.Status.Should().Be(WebhookDeliveryStatus.Failed);
        row.AttemptCount.Should().Be(1);

        // Retry — must actually attempt delivery again since prior was not Succeeded.
        _httpHandler.NextResponse = () => new HttpResponseMessage(HttpStatusCode.OK);
        await _sut.Consume(BuildContext());

        _httpHandler.CallCount.Should().Be(2);
        await _dbContext.Entry(row).ReloadAsync();
        row.Status.Should().Be(WebhookDeliveryStatus.Succeeded);
        row.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task Consume_TwoWebhooksSameEvent_BothDeliverIndependently()
    {
        await SeedWebhook("https://wh.example.com/a");
        await SeedWebhook("https://wh.example.com/b");
        _httpHandler.NextResponse = () => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.Consume(BuildContext());

        _httpHandler.CallCount.Should().Be(2);
        var rows = await _dbContext.WebhookDeliveries.ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Status == WebhookDeliveryStatus.Succeeded);
        rows.Select(r => r.WebhookId).Distinct().Should().HaveCount(2);
    }

    private async Task<Webhook> SeedWebhook(string url)
    {
        var webhook = new Webhook
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Url = url,
            Events = new[] { EventType },
            Secret = "test-secret",
            Status = WebhookStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Webhooks.Add(webhook);
        await _dbContext.SaveChangesAsync();
        return webhook;
    }

    private static ConsumeContext<WebhookDispatchMessage> BuildContext()
    {
        var ctx = Substitute.For<ConsumeContext<WebhookDispatchMessage>>();
        ctx.Message.Returns(new WebhookDispatchMessage
        {
            TenantId = TenantId,
            EmailId = EmailId,
            EventType = EventType,
            MessageId = Guid.NewGuid().ToString(),
            Data = "{}",
            Timestamp = DateTime.UtcNow
        });
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    public void Dispose() => _dbContext.Dispose();

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Func<HttpResponseMessage> NextResponse { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(NextResponse());
        }
    }
}
