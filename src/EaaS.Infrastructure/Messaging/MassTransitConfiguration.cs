using EaaS.Infrastructure.Configuration;
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

                cfg.ReceiveEndpoint("eaas-emails-send", e =>
                {
                    e.PrefetchCount = settings.PrefetchCount;
                    e.ConfigureConsumer<SendEmailConsumer>(context);
                });
            });
        });

        return services;
    }
}
