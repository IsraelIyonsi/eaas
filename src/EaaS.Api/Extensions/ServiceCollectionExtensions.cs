using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using EaaS.Api.Authentication;
using EaaS.Api.Behaviors;
using EaaS.Api.Constants;
using EaaS.Api.Middleware;
using EaaS.Domain.Interfaces;
using EaaS.Shared.Constants;
using EaaS.Shared.Utilities;
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
        services.AddScoped<EaaS.Api.Features.Inbound.Emails.DeleteInboundEmailHandler>();
        services.AddScoped<EaaS.Api.Features.Inbound.Emails.RetryInboundWebhookHandler>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<Program>();

        // Authentication
        // Bind AdminSession options from "Authentication:AdminSession" configuration
        // section so RequireProxyToken / EnforceAfter / SessionSecret are all driven
        // by config. The legacy "AdminSession:SessionSecret" key is still honoured
        // as a fallback so existing deployments keep working during rollout.
        var adminSessionSection = configuration.GetSection("Authentication:AdminSession");
        services.Configure<AdminSessionAuthSchemeOptions>(adminSessionSection);

        services.AddAuthentication(ApiKeyAuthHandler.SchemeName)
            .AddScheme<ApiKeyAuthSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null)
            .AddScheme<AdminSessionAuthSchemeOptions, AdminSessionAuthHandler>(AdminSessionAuthHandler.SchemeName, options =>
            {
                // Config binding above feeds the named options; here we only apply
                // fallbacks and the fail-fast secret-strength guard.
                if (string.IsNullOrEmpty(options.SessionSecret))
                {
                    options.SessionSecret =
                        adminSessionSection["SessionSecret"]
                        ?? configuration["AdminSession:SessionSecret"]
                        ?? string.Empty;
                }

                // RequireProxyToken defaults to true (secure by default). If the
                // config section exists but omits the key, keep the secure default.
                var requireProxyTokenRaw = adminSessionSection["RequireProxyToken"];
                if (!string.IsNullOrEmpty(requireProxyTokenRaw)
                    && bool.TryParse(requireProxyTokenRaw, out var requireProxyToken))
                {
                    options.RequireProxyToken = requireProxyToken;
                }

                // Startup fail-fast: if the signed-token contract is on, the secret
                // MUST exist and be long enough for a meaningful HMAC-SHA256 key.
                // 32 bytes (256 bits) is the minimum we accept.
                if (options.RequireProxyToken)
                {
                    var secretBytes = System.Text.Encoding.UTF8.GetByteCount(options.SessionSecret);
                    if (secretBytes < 32)
                    {
                        throw new InvalidOperationException(
                            "Authentication:AdminSession:SessionSecret must be configured with at least 32 bytes "
                            + "when RequireProxyToken=true. Set a strong secret via configuration/secret store.");
                    }
                }
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicyConstants.SuperAdminPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimNameConstants.AdminRole, AdminRoleConstants.SuperAdmin);
            });

            options.AddPolicy(AuthorizationPolicyConstants.AdminPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimNameConstants.AdminRole, AdminRoleConstants.SuperAdmin, AdminRoleConstants.Admin);
            });

            options.AddPolicy(AuthorizationPolicyConstants.AdminReadPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimNameConstants.AdminRole, AdminRoleConstants.SuperAdmin, AdminRoleConstants.Admin, AdminRoleConstants.ReadOnly);
            });
        });

        // Login rate limiting (ASP.NET built-in, separate from Redis-based per-tenant limiter)
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter(RateLimitConstants.AuthLoginPolicy, opt =>
            {
                opt.Window = RateLimitConstants.AuthLoginWindow;
                opt.PermitLimit = RateLimitConstants.AuthLoginPermitLimit;
                opt.QueueLimit = 0;
            });

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = RateLimitConstants.AuthLoginRetryAfterSeconds;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error":"Too many login attempts. Try again in 1 minute."}""", ct);
            };
        });

        // HTTP clients — wired with SSRF-guarded handler (Finding C3) that pins to
        // the resolved public IP at connect time to prevent DNS rebinding.
        services.AddHttpClient(HttpClientNameConstants.WebhookTest)
            .ConfigurePrimaryHttpMessageHandler(SsrfGuard.CreateGuardedHandler);
        services.AddHttpClient(HttpClientNameConstants.WebhookDispatch)
            .ConfigurePrimaryHttpMessageHandler(SsrfGuard.CreateGuardedHandler);

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
