using System.ComponentModel.DataAnnotations;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Messaging.Contracts;
using EaaS.Infrastructure.Messaging.Observers;
using EaaS.Shared.Constants;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Messaging;

public static class MassTransitConfiguration
{
    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services)
    {
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<SendEmailConsumer>();
            bus.AddConsumer<WebhookDispatchConsumer>();
            bus.AddConsumer<InboundEmailConsumer>();
            bus.AddConsumer<BatchEmailConsumer>();

            bus.UsingRabbitMq((context, cfg) =>
            {
                var settings = context.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

                cfg.Host(settings.Host, (ushort)settings.Port, settings.VirtualHost, h =>
                {
                    h.Username(settings.Username);
                    h.Password(settings.Password);
                });

                // -------------------------------------------------------------------
                // Email Send Queue — primary outbound pipeline (140+ msg/s target)
                // -------------------------------------------------------------------
                cfg.ReceiveEndpoint(MessagingConstants.EmailSendQueue, e =>
                {
                    /// PrefetchCount = 50: Allows RabbitMQ to push 50 messages ahead of ACKs.
                    /// At 140 msg/s with ~100ms SES latency, 25 concurrent workers need a buffer
                    /// of 2x concurrency to avoid idle time waiting for the next message.
                    e.PrefetchCount = settings.EmailSendPrefetchCount;

                    /// ConcurrencyLimit = 25: Caps parallel SES API calls. AWS SES connections
                    /// degrade above ~30 concurrent requests per host; 25 provides headroom.
                    e.UseConcurrencyLimit(settings.EmailSendConcurrency);

                    /// RateLimit = 100/s: Enforces SES account-level burst sending rate.
                    /// Prevents 429 throttling from SES which would trigger retries and
                    /// amplify load. Adjust as SES limits are raised via AWS support.
                    e.UseRateLimit(settings.EmailSendRateLimit, TimeSpan.FromSeconds(1));

                    /// Circuit breaker protects SES from cascading failures.
                    /// Trips at 15% failure rate (min 10 messages) and holds open for 5 min,
                    /// giving SES time to recover from transient outages.
                    e.UseCircuitBreaker(cb =>
                    {
                        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                        cb.TripThreshold = 15;
                        cb.ActiveThreshold = 10;
                        cb.ResetInterval = TimeSpan.FromMinutes(5);
                    });

                    /// Delayed redelivery for transient failures (SES throttling, network blips).
                    /// Progressive backoff from 1 min to 24 hours ensures delivery without
                    /// overwhelming SES during extended outages.
                    e.UseDelayedRedelivery(r => r.Intervals(
                        TimeSpan.FromMinutes(1),
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(30),
                        TimeSpan.FromHours(2),
                        TimeSpan.FromHours(24)));

                    /// Exponential backoff with jitter for immediate retries.
                    /// 5 attempts from 1s to 5min with 5s delta prevents retry storms
                    /// when multiple consumers hit the same transient error.
                    /// ValidationException is ignored — no point retrying invalid data.
                    e.UseMessageRetry(r =>
                    {
                        r.Exponential(5,
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromMinutes(5),
                            TimeSpan.FromSeconds(5));
                        r.Ignore<ValidationException>();
                    });

                    e.ConfigureConsumer<SendEmailConsumer>(context);
                });

                // -------------------------------------------------------------------
                // Email Send Priority Queue — OTP, password resets, security alerts
                // -------------------------------------------------------------------
                cfg.ReceiveEndpoint(MessagingConstants.EmailSendPriorityQueue, e =>
                {
                    /// Lower prefetch than standard queue: priority messages are fewer but
                    /// must not be starved by a large prefetch buffer of slow messages.
                    e.PrefetchCount = 20;

                    /// Dedicated concurrency of 10 ensures priority messages always have
                    /// available worker threads regardless of standard queue pressure.
                    e.UseConcurrencyLimit(10);

                    /// No rate limiter on priority queue — these are time-critical messages
                    /// (OTP codes expire). SES headroom from the standard queue's rate limit
                    /// provides the budget for priority sends.

                    e.UseCircuitBreaker(cb =>
                    {
                        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                        cb.TripThreshold = 15;
                        cb.ActiveThreshold = 5;
                        cb.ResetInterval = TimeSpan.FromMinutes(3);
                    });

                    e.UseDelayedRedelivery(r => r.Intervals(
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromMinutes(2),
                        TimeSpan.FromMinutes(10)));

                    e.UseMessageRetry(r =>
                    {
                        r.Exponential(3,
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(30),
                            TimeSpan.FromSeconds(2));
                        r.Ignore<ValidationException>();
                    });

                    e.ConfigureConsumer<SendEmailConsumer>(context);
                });

                // -------------------------------------------------------------------
                // Email Send Batch Queue — marketing, newsletters, bulk sends
                // -------------------------------------------------------------------
                cfg.ReceiveEndpoint(MessagingConstants.EmailSendBatchQueue, e =>
                {
                    /// Prefetch 100 to feed the batch consumer which groups up to 50 messages.
                    /// 2x batch size ensures the next batch is ready when the current completes.
                    e.PrefetchCount = 100;
                    e.UseConcurrencyLimit(5);

                    e.UseCircuitBreaker(cb =>
                    {
                        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                        cb.TripThreshold = 15;
                        cb.ActiveThreshold = 10;
                        cb.ResetInterval = TimeSpan.FromMinutes(5);
                    });

                    e.UseDelayedRedelivery(r => r.Intervals(
                        TimeSpan.FromMinutes(1),
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(30),
                        TimeSpan.FromHours(2),
                        TimeSpan.FromHours(24)));

                    e.UseMessageRetry(r =>
                    {
                        r.Exponential(5,
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromMinutes(5),
                            TimeSpan.FromSeconds(5));
                        r.Ignore<ValidationException>();
                    });

                    /// Batch consumer: groups up to 50 messages with a 500ms window.
                    /// Reduces DB round trips from 50 individual INSERTs to 1 batched SaveChanges.
                    e.ConfigureConsumer<BatchEmailConsumer>(context, c =>
                    {
                        c.Options<BatchOptions>(o => o
                            .SetMessageLimit(50)
                            .SetTimeLimit(TimeSpan.FromMilliseconds(500)));
                    });
                });

                // -------------------------------------------------------------------
                // Webhook Dispatch Queue — delivers events to customer endpoints
                // -------------------------------------------------------------------
                cfg.ReceiveEndpoint(MessagingConstants.WebhookDispatchQueue, e =>
                {
                    /// PrefetchCount = 30: Webhook HTTP calls have variable latency (50ms–10s).
                    /// 2x concurrency provides enough buffer without hoarding messages
                    /// that other consumers could process.
                    e.PrefetchCount = settings.WebhookPrefetchCount;

                    /// ConcurrencyLimit = 15: Limits outbound HTTP connections to avoid
                    /// exhausting the HttpClient connection pool or overwhelming customer endpoints.
                    e.UseConcurrencyLimit(settings.WebhookConcurrency);

                    /// RateLimit = 50/s: Prevents thundering herd when a burst of email events
                    /// triggers webhooks for all 100 customers simultaneously.
                    e.UseRateLimit(settings.WebhookRateLimit, TimeSpan.FromSeconds(1));

                    e.UseCircuitBreaker(cb =>
                    {
                        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                        cb.TripThreshold = 15;
                        cb.ActiveThreshold = 10;
                        cb.ResetInterval = TimeSpan.FromMinutes(5);
                    });

                    e.UseDelayedRedelivery(r => r.Intervals(
                        TimeSpan.FromMinutes(1),
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(30),
                        TimeSpan.FromHours(2),
                        TimeSpan.FromHours(24)));

                    /// Aggressive retry for webhooks — customer endpoints are unreliable.
                    /// Longer intervals (10s–810s) with 5 attempts before dead-lettering.
                    e.UseMessageRetry(r =>
                    {
                        r.Exponential(5,
                            TimeSpan.FromSeconds(10),
                            TimeSpan.FromMinutes(15),
                            TimeSpan.FromSeconds(30));
                        r.Ignore<ValidationException>();
                    });

                    e.ConfigureConsumer<WebhookDispatchConsumer>(context);
                });

                // -------------------------------------------------------------------
                // Inbound Email Process Queue — receives from SES Inbound via S3
                // -------------------------------------------------------------------
                cfg.ReceiveEndpoint(MessagingConstants.InboundEmailProcessQueue, e =>
                {
                    /// PrefetchCount = 20: Each inbound message involves S3 fetch + MIME parse +
                    /// attachment storage. Memory-intensive, so keep prefetch conservative.
                    e.PrefetchCount = settings.InboundPrefetchCount;

                    /// ConcurrencyLimit = 10: MIME parsing of large emails with attachments
                    /// can spike memory. 10 concurrent parses caps memory at ~500MB worst case.
                    e.UseConcurrencyLimit(settings.InboundConcurrency);

                    /// No rate limiter: Inbound is event-driven from SES and naturally bursty.
                    /// The concurrency limit provides sufficient back-pressure.

                    e.UseCircuitBreaker(cb =>
                    {
                        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                        cb.TripThreshold = 15;
                        cb.ActiveThreshold = 10;
                        cb.ResetInterval = TimeSpan.FromMinutes(5);
                    });

                    e.UseDelayedRedelivery(r => r.Intervals(
                        TimeSpan.FromMinutes(1),
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(30),
                        TimeSpan.FromHours(2),
                        TimeSpan.FromHours(24)));

                    e.UseMessageRetry(r =>
                    {
                        r.Exponential(5,
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromMinutes(5),
                            TimeSpan.FromSeconds(10));
                        r.Ignore<ValidationException>();
                    });

                    e.ConfigureConsumer<InboundEmailConsumer>(context);
                });

                // Register consume observer for structured logging and metrics.
                // Wired at the bus level so it intercepts all consumers across all endpoints.
                var observer = context.GetRequiredService<ConsumeObserver>();
                cfg.ConnectConsumeObserver(observer);
            });
        });

        // Register the observer in DI for resolution inside the bus factory
        services.AddSingleton<ConsumeObserver>();

        return services;
    }

    /// <summary>
    /// Registers MassTransit in publish-only mode (no consumers).
    /// Used by services that only need to publish messages (e.g., WebhookProcessor).
    /// </summary>
    public static IServiceCollection AddMassTransitPublishOnly(
        this IServiceCollection services)
    {
        services.AddMassTransit(bus =>
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                var settings = context.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

                cfg.Host(settings.Host, (ushort)settings.Port, settings.VirtualHost, h =>
                {
                    h.Username(settings.Username);
                    h.Password(settings.Password);
                });
            });
        });

        return services;
    }
}
