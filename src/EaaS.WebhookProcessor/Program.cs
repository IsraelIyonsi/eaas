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

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.MapHealthChecks("/health");

    app.MapGet("/", () => Results.Ok(new { Service = "EaaS Webhook Processor", Status = "Running" }));

    // SNS webhook endpoint placeholder
    app.MapPost("/webhooks/sns", () => Results.Ok());

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
