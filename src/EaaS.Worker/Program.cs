using Amazon;
using Amazon.SimpleEmailV2;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Services;
using EaaS.Shared.Utilities;
using EaaS.Worker.Jobs;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EaaS Worker");

    var builder = Host.CreateDefaultBuilder(args);

    builder.ConfigureServices((context, services) =>
    {
        services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .WriteTo.GrafanaLoki(
                Environment.GetEnvironmentVariable("LOKI_URL") ?? "http://loki:3100",
                labels: new[]
                {
                    new LokiLabel { Key = "app", Value = "eaas-worker" },
                    new LokiLabel { Key = "env", Value = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development" }
                }));

        // Infrastructure services (DbContext, Redis, MassTransit consumers)
        services.AddInfrastructure(context.Configuration);

        // SSRF guard (C3 rev-4): ops-tunable kill-switch + CIDR/host allowlists.
        services.Configure<SsrfGuardOptions>(context.Configuration.GetSection(SsrfGuardOptions.SectionName));
        services.AddSingleton<SsrfGuardService>();

        // Email delivery: SMTP (Mailpit) for local dev, SES for production
        services.AddEmailProvider(context.Configuration);

        // Inbound email services (S3 storage, MIME parser)
        services.AddInboundServices(context.Configuration);

        // Template rendering
        services.AddSingleton<ITemplateRenderingService, TemplateRenderingService>();

        // HTTP client for webhook dispatch — SSRF-guarded handler pins to
        // validated public IP at connect time (Finding C3, prevents DNS rebinding).
        services.AddHttpClient("WebhookDispatch")
            .ConfigurePrimaryHttpMessageHandler(SsrfGuard.CreateGuardedHandler);

        // Scheduled email background job
        services.AddHostedService<ScheduledEmailJob>();
    });

    var host = builder.Build();

    // Expose Prometheus metrics on port 9090 (Worker has no HTTP pipeline)
    var metricServer = new Prometheus.MetricServer(port: 9090);
    metricServer.Start();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
