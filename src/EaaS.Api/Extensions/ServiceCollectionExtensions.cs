using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using EaaS.Api.Authentication;
using EaaS.Api.Behaviors;
using EaaS.Api.Middleware;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using FluentValidation;
using MediatR;

namespace EaaS.Api.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly string[] DefaultCorsOrigins = ["http://localhost:3000", "http://localhost:5001"];

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(
                    configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? DefaultCorsOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
        });

        // Application services
        services.AddSingleton<ITemplateRenderingService, TemplateRenderingService>();
        services.AddScoped<EaaS.Api.Services.SuppressionChecker>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<Program>();

        // Authentication
        services.AddAuthentication(ApiKeyAuthHandler.SchemeName)
            .AddScheme<ApiKeyAuthSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null)
            .AddScheme<AdminSessionAuthSchemeOptions, AdminSessionAuthHandler>(AdminSessionAuthHandler.SchemeName, options =>
            {
                options.SessionSecret = configuration["AdminSession:SessionSecret"] ?? string.Empty;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SuperAdminPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("AdminRole", "SuperAdmin");
            });

            options.AddPolicy("AdminPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("AdminRole", "SuperAdmin", "Admin");
            });

            options.AddPolicy("AdminReadPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("AdminRole", "SuperAdmin", "Admin", "ReadOnly");
            });
        });

        // Login rate limiting (ASP.NET built-in, separate from Redis-based per-tenant limiter)
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("AuthLogin", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 5;
                opt.QueueLimit = 0;
            });

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error":"Too many login attempts. Try again in 1 minute."}""", ct);
            };
        });

        // HTTP clients
        services.AddHttpClient("WebhookTest");
        services.AddHttpClient("WebhookDispatch");

        // Exception handling
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // OpenAPI
        services.AddOpenApi();

        // Health checks
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(name: "postgresql");

        return services;
    }
}
