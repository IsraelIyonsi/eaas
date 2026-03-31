using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using EaaS.WebhookProcessor.Handlers;
using Microsoft.EntityFrameworkCore;
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
        string token,
        HttpContext httpContext,
        ITrackingTokenService tokenService,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        // 1x1 transparent GIF (43 bytes)
        var transparentGif = new byte[]
        {
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00,
            0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44,
            0x01, 0x00, 0x3B
        };

        var data = tokenService.ValidateToken(token);
        if (data is not null && data.EventType == "open")
        {
            try
            {
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // Atomic update: only set OpenedAt if null
                await dbContext.Emails
                    .Where(e => e.Id == data.EmailId && e.OpenedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.OpenedAt, DateTime.UtcNow), cancellationToken);

                dbContext.EmailEvents.Add(new EmailEvent
                {
                    Id = Guid.NewGuid(),
                    EmailId = data.EmailId,
                    EventType = EventType.Opened,
                    Data = JsonSerializer.Serialize(new { userAgent, ip }),
                    CreatedAt = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Tracking failures should not break the pixel response
            }
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Results.File(transparentGif, "image/gif");
    });

    // Click tracking endpoint
    app.MapGet("/track/click/{token}", async (
        string token,
        HttpContext httpContext,
        AppDbContext dbContext,
        ITrackingTokenService tokenService,
        CancellationToken cancellationToken) =>
    {
        // Look up by short token in tracking_links table
        var trackingLink = await dbContext.TrackingLinks
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (trackingLink is null)
        {
            // Fallback: try validating as HMAC token (backward compatibility)
            var data = tokenService.ValidateToken(token);
            if (data is not null && data.EventType == "click" && !string.IsNullOrWhiteSpace(data.OriginalUrl))
            {
                return Results.Redirect(data.OriginalUrl);
            }

            return Results.NotFound();
        }

        try
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Record click time on the tracking link
            if (trackingLink.ClickedAt is null)
                trackingLink.ClickedAt = DateTime.UtcNow;

            // Atomic update: only set ClickedAt if null on the email
            await dbContext.Emails
                .Where(e => e.Id == trackingLink.EmailId && e.ClickedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ClickedAt, DateTime.UtcNow), cancellationToken);

            dbContext.EmailEvents.Add(new EmailEvent
            {
                Id = Guid.NewGuid(),
                EmailId = trackingLink.EmailId,
                EventType = EventType.Clicked,
                Data = JsonSerializer.Serialize(new
                {
                    originalUrl = trackingLink.OriginalUrl,
                    userAgent,
                    ip
                }),
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Tracking failures should not break the redirect
        }

        return Results.Redirect(trackingLink.OriginalUrl);
    });

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
