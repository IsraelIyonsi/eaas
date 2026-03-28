using Amazon;
using Amazon.SimpleEmailV2;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Services;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EaaS Worker");

    var builder = Host.CreateDefaultBuilder(args);

    builder.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName());

    builder.ConfigureServices((context, services) =>
    {
        // Infrastructure services (DbContext, Redis, MassTransit consumers)
        services.AddInfrastructure(context.Configuration);

        // AWS SES client
        var sesSettings = context.Configuration.GetSection(SesSettings.SectionName).Get<SesSettings>() ?? new SesSettings();
        services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ =>
            new AmazonSimpleEmailServiceV2Client(
                sesSettings.AccessKeyId,
                sesSettings.SecretAccessKey,
                RegionEndpoint.GetBySystemName(sesSettings.Region)));
        services.AddSingleton<IEmailDeliveryService, SesEmailService>();

        // Template rendering
        services.AddSingleton<ITemplateRenderingService, TemplateRenderingService>();
    });

    var host = builder.Build();

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
