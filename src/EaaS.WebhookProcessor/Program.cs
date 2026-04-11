using EaaS.Infrastructure;
using EaaS.WebhookProcessor.Handlers;
using EaaS.WebhookProcessor.Services;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EaaS Webhook Processor");

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.GrafanaLoki(
            Environment.GetEnvironmentVariable("LOKI_URL") ?? "http://loki:3100",
            labels: new[]
            {
                new LokiLabel { Key = "app", Value = "eaas-webhook-processor" },
                new LokiLabel { Key = "env", Value = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development" }
            }));

    // Infrastructure services (DbContext, Redis, MassTransit publish-only for webhook dispatch)
    builder.Services.AddInfrastructure(builder.Configuration, publishOnly: true);

    // SNS webhook handlers
    builder.Services.AddScoped<SnsMessageHandler>();
    builder.Services.AddScoped<SnsInboundHandler>();
    builder.Services.AddScoped<BounceHandler>();
    builder.Services.AddScoped<ComplaintHandler>();
    builder.Services.AddScoped<DeliveryHandler>();

    // Tracking handlers + shared services
    builder.Services.AddScoped<OpenTrackingHandler>();
    builder.Services.AddScoped<ClickTrackingHandler>();
    builder.Services.AddScoped<RecipientSuppressor>();

    // HTTP client for SNS subscription confirmation
    builder.Services.AddHttpClient("SnsConfirmation");

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Liveness probe: always returns 200 if the process is running and Kestrel is accepting requests.
    // Used by Docker HEALTHCHECK to determine if the container is alive.
    app.MapGet("/health", () => Results.Ok("Healthy"));

    // Readiness probe: includes MassTransit bus health + any registered IHealthCheck.
    // Use this to check if the service is ready to handle traffic (e.g., RabbitMQ connected).
    app.MapHealthChecks("/health/ready");

    app.MapGet("/", () => Results.Ok(new { Service = "EaaS Webhook Processor", Status = "Running" }));

    app.MapMetrics("/metrics");

    // SNS webhook endpoint
    app.MapPost("/webhooks/sns", async (HttpContext httpContext, SnsMessageHandler handler, CancellationToken cancellationToken) =>
    {
        return await handler.HandleAsync(httpContext.Request, cancellationToken);
    });

    // SNS inbound email endpoint (separate from outbound notifications)
    app.MapPost("/webhooks/sns/inbound", async (HttpContext httpContext, SnsInboundHandler handler, CancellationToken ct) =>
    {
        return await handler.HandleAsync(httpContext.Request, ct);
    });

    // Open tracking endpoint
    app.MapGet("/track/open/{token}", async (
        string token, HttpContext httpContext, OpenTrackingHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(token, httpContext, ct));

    // Click tracking endpoint
    app.MapGet("/track/click/{token}", async (
        string token, HttpContext httpContext, ClickTrackingHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(token, httpContext, ct));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Webhook Processor terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
