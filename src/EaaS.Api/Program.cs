using System.Reflection;
using Amazon;
using Amazon.SimpleEmailV2;
using EaaS.Api.Authentication;
using EaaS.Api.Behaviors;
using EaaS.Api.Commands;
using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Features.Domains;
using EaaS.Api.Features.Emails;
using EaaS.Api.Features.Analytics;
using EaaS.Api.Features.Suppressions;
using EaaS.Api.Features.Templates;
using EaaS.Api.Features.Webhooks;
using EaaS.Api.Middleware;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Services;
using FluentValidation;
using MediatR;
using EaaS.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
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

    // Email delivery: SMTP (Mailpit) for local dev, SES for production
    builder.Services.AddEmailProvider(builder.Configuration);
    builder.Services.AddSingleton<ITemplateRenderingService, TemplateRenderingService>();
    builder.Services.AddScoped<EaaS.Api.Services.SuppressionChecker>();

    // MediatR
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Authentication
    builder.Services.AddAuthentication(ApiKeyAuthHandler.SchemeName)
        .AddScheme<ApiKeyAuthSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null);
    builder.Services.AddAuthorization();

    // HTTP client for webhook testing
    builder.Services.AddHttpClient("WebhookTest");
    builder.Services.AddHttpClient("WebhookDispatch");

    // Exception handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "EaaS API",
            Version = "v1",
            Description = "Email as a Service - Transactional Email API"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "API Key",
            Description = "Enter your API key"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);
    });

    // Health checks
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>(name: "postgresql");

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "EaaS API v1");
    });

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    // Liveness probe: always returns 200 if Kestrel is accepting requests (Docker HEALTHCHECK target)
    app.MapGet("/health", () => Results.Ok("Healthy"));

    // Readiness probe: includes DB, MassTransit bus, and any registered IHealthCheck
    app.MapHealthChecks("/health/ready");

    app.MapGet("/", () => Results.Ok(new { Service = "EaaS API", Status = "Running" }));

    // API Key endpoints
    var keysGroup = app.MapGroup("/api/v1/keys")
        .RequireAuthorization()
        .WithTags("API Keys");

    CreateApiKeyEndpoint.Map(keysGroup);
    RevokeApiKeyEndpoint.Map(keysGroup);
    ListApiKeysEndpoint.Map(keysGroup);
    RotateApiKeyEndpoint.Map(keysGroup);

    // Domain endpoints
    var domainsGroup = app.MapGroup("/api/v1/domains")
        .RequireAuthorization()
        .WithTags("Domains");

    AddDomainEndpoint.Map(domainsGroup);
    ListDomainsEndpoint.Map(domainsGroup);
    VerifyDomainEndpoint.Map(domainsGroup);
    RemoveDomainEndpoint.Map(domainsGroup);

    // Email endpoints
    var emailsGroup = app.MapGroup("/api/v1/emails")
        .RequireAuthorization()
        .WithTags("Emails");

    SendEmailEndpoint.Map(emailsGroup);
    SendBatchEndpoint.Map(emailsGroup);
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
    PreviewTemplateEndpoint.Map(templatesGroup);

    // Suppression endpoints
    var suppressionsGroup = app.MapGroup("/api/v1/suppressions")
        .RequireAuthorization()
        .WithTags("Suppressions");

    ListSuppressionsEndpoint.Map(suppressionsGroup);
    AddSuppressionEndpoint.Map(suppressionsGroup);
    RemoveSuppressionEndpoint.Map(suppressionsGroup);

    // Analytics endpoints
    var analyticsGroup = app.MapGroup("/api/v1/analytics")
        .RequireAuthorization()
        .WithTags("Analytics");

    GetAnalyticsSummaryEndpoint.Map(analyticsGroup);
    GetAnalyticsTimelineEndpoint.Map(analyticsGroup);

    // Webhook endpoints
    var webhooksGroup = app.MapGroup("/api/v1/webhooks")
        .RequireAuthorization()
        .WithTags("Webhooks");

    CreateWebhookEndpoint.Map(webhooksGroup);
    ListWebhooksEndpoint.Map(webhooksGroup);
    GetWebhookEndpoint.Map(webhooksGroup);
    UpdateWebhookEndpoint.Map(webhooksGroup);
    DeleteWebhookEndpoint.Map(webhooksGroup);
    TestWebhookEndpoint.Map(webhooksGroup);

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
