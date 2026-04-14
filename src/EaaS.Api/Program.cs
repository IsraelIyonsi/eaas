using EaaS.Api.Commands;
using EaaS.Api.Extensions;
using EaaS.Infrastructure;
using EaaS.Infrastructure.Payments;
using EaaS.Shared.Utilities;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EaaS API");

    var builder = WebApplication.CreateBuilder(args);

    // Handle seed commands before building the full app
    if (args.Contains("seed"))
    {
        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName());

        builder.Services.AddInfrastructure(builder.Configuration, includeMassTransit: false);

        var seedApp = builder.Build();
        var exitCode = await SeedCommand.ExecuteAsync(args, seedApp.Services);
        return;
    }

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.GrafanaLoki(
            Environment.GetEnvironmentVariable("LOKI_URL") ?? "http://loki:3100",
            labels: new[]
            {
                new LokiLabel { Key = "app", Value = "eaas-api" },
                new LokiLabel { Key = "env", Value = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development" }
            }));

    // Infrastructure services (DbContext, Redis, MassTransit)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Email delivery: provider-agnostic abstraction (SES default, SMTP for local dev).
    builder.Services.AddEmailProviders(builder.Configuration);

    // Inbound email services (S3 storage, MIME parser)
    builder.Services.AddInboundServices(builder.Configuration);

    // Payment providers (Stripe, PayStack, Flutterwave, PayPal)
    builder.Services.AddPaymentProviders(builder.Configuration);

    // API services (MediatR, validation, auth, Swagger, health checks)
    builder.Services.AddApiServices(builder.Configuration);

    var app = builder.Build();

    app.UseApiMiddleware();
    app.MapApiEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory in integration tests
namespace EaaS.Api
{
    public partial class Program;
}
