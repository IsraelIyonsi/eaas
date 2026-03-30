using EaaS.Infrastructure.Configuration;
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

            bus.UsingRabbitMq((context, cfg) =>
            {
                var settings = context.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

                cfg.Host(settings.Host, (ushort)settings.Port, settings.VirtualHost, h =>
                {
                    h.Username(settings.Username);
                    h.Password(settings.Password);
                });

                cfg.UseMessageRetry(r =>
                    r.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(30)));

                cfg.ReceiveEndpoint(MessagingConstants.EmailSendQueue, e =>
                {
                    e.PrefetchCount = settings.PrefetchCount;
                    e.ConfigureConsumer<SendEmailConsumer>(context);
                });

                cfg.ReceiveEndpoint(MessagingConstants.WebhookDispatchQueue, e =>
                {
                    e.PrefetchCount = settings.PrefetchCount;
                    e.UseMessageRetry(r =>
                        r.Intervals(
                            TimeSpan.FromSeconds(10),
                            TimeSpan.FromSeconds(30),
                            TimeSpan.FromSeconds(90),
                            TimeSpan.FromSeconds(270),
                            TimeSpan.FromSeconds(810)));
                    e.ConfigureConsumer<WebhookDispatchConsumer>(context);
                });
            });
        });

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
