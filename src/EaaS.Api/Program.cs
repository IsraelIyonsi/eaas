using Amazon;
using Amazon.SimpleEmailV2;
using EaaS.Api.Authentication;
using EaaS.Api.Behaviors;
using EaaS.Api.Commands;
using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Features.Domains;
using EaaS.Api.Features.Emails;
using EaaS.Api.Features.Templates;
using EaaS.Api.Middleware;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Services;
using FluentValidation;
using MediatR;
using EaaS.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;

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
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName());

        builder.Services.AddInfrastructure(builder.Configuration);

        var seedApp = builder.Build();
        var exitCode = await SeedCommand.ExecuteAsync(args, seedApp.Services);
        return;
    }

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName());

    // Infrastructure services (DbContext, Redis, MassTransit)
    builder.Services.AddInfrastructure(builder.Configuration);

    // AWS SES client
    var sesSettings = builder.Configuration.GetSection(SesSettings.SectionName).Get<SesSettings>() ?? new SesSettings();
    builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ =>
        new AmazonSimpleEmailServiceV2Client(
            sesSettings.AccessKeyId,
            sesSettings.SecretAccessKey,
            RegionEndpoint.GetBySystemName(sesSettings.Region)));
    builder.Services.AddSingleton<IEmailDeliveryService, SesEmailService>();
    builder.Services.AddSingleton<ITemplateRenderingService, TemplateRenderingService>();

    // MediatR
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Authentication
    builder.Services.AddAuthentication(ApiKeyAuthHandler.SchemeName)
        .AddScheme<ApiKeyAuthSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null);
    builder.Services.AddAuthorization();

    // Exception handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Health checks
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>(name: "postgresql");

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    // Health check (no auth)
    app.MapHealthChecks("/health");

    app.MapGet("/", () => Results.Ok(new { Service = "EaaS API", Status = "Running" }));

    // API Key endpoints
    var keysGroup = app.MapGroup("/api/v1/keys")
        .RequireAuthorization()
        .WithTags("API Keys");

    CreateApiKeyEndpoint.Map(keysGroup);
    RevokeApiKeyEndpoint.Map(keysGroup);
    ListApiKeysEndpoint.Map(keysGroup);

    // Domain endpoints
    var domainsGroup = app.MapGroup("/api/v1/domains")
        .RequireAuthorization()
        .WithTags("Domains");

    AddDomainEndpoint.Map(domainsGroup);
    ListDomainsEndpoint.Map(domainsGroup);
    VerifyDomainEndpoint.Map(domainsGroup);

    // Email endpoints
    var emailsGroup = app.MapGroup("/api/v1/emails")
        .RequireAuthorization()
        .WithTags("Emails");

    SendEmailEndpoint.Map(emailsGroup);
    GetEmailEndpoint.Map(emailsGroup);
    ListEmailsEndpoint.Map(emailsGroup);

    // Template endpoints
    var templatesGroup = app.MapGroup("/api/v1/templates")
        .RequireAuthorization()
        .WithTags("Templates");

    CreateTemplateEndpoint.Map(templatesGroup);
    GetTemplateEndpoint.Map(templatesGroup);
    ListTemplatesEndpoint.Map(templatesGroup);
    UpdateTemplateEndpoint.Map(templatesGroup);
    DeleteTemplateEndpoint.Map(templatesGroup);

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
