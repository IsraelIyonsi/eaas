using EaaS.Infrastructure;
using EaaS.Shared.Utilities;
using EaaS.WebhookProcessor.Configuration;
using EaaS.WebhookProcessor.Handlers;
using EaaS.WebhookProcessor.Services;
using Microsoft.Extensions.Options;
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

    // Console sink is ALWAYS active so `docker logs eaas-webhook-processor` surfaces rejection
    // reasons even when Loki isn't deployed. Operability bug: previously a missing Loki backend
    // silenced the entire service, masking 403 root causes behind zero log output.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter())
        .WriteTo.GrafanaLoki(
            Environment.GetEnvironmentVariable("LOKI_URL") ?? "http://loki:3100",
            labels: new[]
            {
                new LokiLabel { Key = "app", Value = "eaas-webhook-processor" },
                new LokiLabel { Key = "env", Value = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development" }
            }));

    // Infrastructure services (DbContext, Redis, MassTransit publish-only for webhook dispatch)
    builder.Services.AddInfrastructure(builder.Configuration, publishOnly: true);

    // SSRF guard (C3 rev-4): ops-tunable kill-switch + CIDR/host allowlists.
    builder.Services.Configure<SsrfGuardOptions>(builder.Configuration.GetSection(SsrfGuardOptions.SectionName));
    builder.Services.AddSingleton<SsrfGuardService>();

    // SNS webhook handlers
    builder.Services.AddScoped<SnsMessageHandler>();
    builder.Services.AddScoped<SnsInboundHandler>();
    builder.Services.AddScoped<BounceHandler>();
    builder.Services.AddScoped<ComplaintHandler>();
    builder.Services.AddScoped<DeliveryHandler>();
    builder.Services.AddScoped<IBounceHandler>(sp => sp.GetRequiredService<BounceHandler>());
    builder.Services.AddScoped<IComplaintHandler>(sp => sp.GetRequiredService<ComplaintHandler>());
    builder.Services.AddScoped<IDeliveryHandler>(sp => sp.GetRequiredService<DeliveryHandler>());

    // Tracking handlers + shared services
    builder.Services.AddScoped<OpenTrackingHandler>();
    builder.Services.AddScoped<ClickTrackingHandler>();
    builder.Services.AddScoped<RecipientSuppressor>();

    // HTTP client for SNS subscription confirmation — an attacker-signed message could
    // point SubscribeURL at internal IPs. Guarded handler blocks RFC1918 / loopback /
    // IMDS at connect time; the handler in SnsInboundHandler also enforces an anchored
    // host allowlist (^https://sns\.<region>\.amazonaws\.com/) for defense in depth.
    builder.Services.AddHttpClient("sns-subscribe", c =>
        {
            // Bounded overall timeout — a slow-loris SubscribeURL mustn't pin a worker thread.
            c.Timeout = TimeSpan.FromSeconds(10);
            // SNS confirmation responses are tiny; refuse anything past 64 KB as abuse.
            c.MaxResponseContentBufferSize = 64 * 1024;
        })
        .ConfigurePrimaryHttpMessageHandler(SsrfGuard.CreateGuardedHandler);

    // HTTP client for fetching SNS signing certificates — same SSRF concern.
    // MaxResponseContentBufferSize hard-caps response body to 64 KB (AWS PEM certs are ~2 KB),
    // defending against a compromised/stalled upstream that streams an unbounded body.
    builder.Services.AddHttpClient("SnsSigningCert", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(10);
            c.MaxResponseContentBufferSize = 64 * 1024;
        })
        .ConfigurePrimaryHttpMessageHandler(SsrfGuard.CreateGuardedHandler);

    // SNS webhook options (kill switch, cache tuning, dedup TTL, body cap).
    builder.Services.Configure<SnsWebhookOptions>(builder.Configuration.GetSection(SnsWebhookOptions.SectionName));

    // Signature verification (singleton so cert cache is shared across requests)
    builder.Services.AddSingleton<SnsSignatureVerifier>();

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

    // AWS SNS payloads are bounded at 150 KB (per AWS SNS message-size limit). Anything larger is a
    // probe/DoS-adjacent request — reject with 413 before we allocate a StreamReader + buffer the body.
    // Limit is read from IOptionsMonitor<SnsWebhookOptions> so ops can tune via config hot-reload
    // with no process restart (matches the kill switch's zero-downtime contract).

    static async Task<IResult> EnforceSnsBodyLimitAsync<THandler>(
        HttpContext httpContext,
        THandler handler,
        long maxBodyBytes,
        Func<THandler, HttpRequest, CancellationToken, Task<IResult>> invoke,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.ContentLength is long declared && declared > maxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // ContentLength may be absent (chunked). Enforce the cap with a hard read limit so an
        // attacker can't stream an unbounded body past our check.
        httpContext.Request.Body = new LengthLimitingStream(httpContext.Request.Body, maxBodyBytes);
        try
        {
            return await invoke(handler, httpContext.Request, cancellationToken);
        }
        catch (PayloadTooLargeException)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
    }

    // SNS webhook endpoint
    app.MapPost("/webhooks/sns", async (HttpContext httpContext, SnsMessageHandler handler, IOptionsMonitor<SnsWebhookOptions> opts, CancellationToken cancellationToken) =>
        await EnforceSnsBodyLimitAsync(httpContext, handler, opts.CurrentValue.MaxBodyBytes, (h, r, ct) => h.HandleAsync(r, ct), cancellationToken));

    // SNS inbound email endpoint (separate from outbound notifications)
    app.MapPost("/webhooks/sns/inbound", async (HttpContext httpContext, SnsInboundHandler handler, IOptionsMonitor<SnsWebhookOptions> opts, CancellationToken ct) =>
        await EnforceSnsBodyLimitAsync(httpContext, handler, opts.CurrentValue.MaxBodyBytes, (h, r, c) => h.HandleAsync(r, c), ct));

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
