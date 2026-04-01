using EaaS.Domain.Interfaces;
using EaaS.Infrastructure;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Services;
using EaaS.WebhookProcessor.Handlers;
using EaaS.WebhookProcessor.Services;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EaaS Webhook Processor");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Infrastructure services (DbContext, Redis, MassTransit publish-only for webhook dispatch)
    builder.Services.AddInfrastructure(builder.Configuration, publishOnly: true);

    // Tracking settings and services
    builder.Services.Configure<TrackingSettings>(builder.Configuration.GetSection(TrackingSettings.SectionName));
    builder.Services.AddSingleton<ITrackingTokenService, TrackingTokenService>();

    // SNS webhook handlers
    builder.Services.AddScoped<SnsMessageHandler>();
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

    // SNS webhook endpoint
    app.MapPost("/webhooks/sns", async (HttpContext httpContext, SnsMessageHandler handler, CancellationToken cancellationToken) =>
    {
        return await handler.HandleAsync(httpContext.Request, cancellationToken);
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
