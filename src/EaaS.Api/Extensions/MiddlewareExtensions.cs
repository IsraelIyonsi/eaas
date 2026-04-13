using EaaS.Api.Constants;
using EaaS.Api.Middleware;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;

namespace EaaS.Api.Extensions;

public static class MiddlewareExtensions
{
    public static WebApplication UseApiMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.Title = "EaaS API";
                options.Theme = ScalarTheme.BluePlanet;
            });
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.UseMiddleware<RateLimitingMiddleware>();

        // Liveness probe: always returns 200 if Kestrel is accepting requests (Docker HEALTHCHECK target)
        app.MapGet(MiddlewarePathConstants.HealthCheck, () => Results.Ok("Healthy"));

        // Readiness probe: includes DB, MassTransit bus, and any registered IHealthCheck
        app.MapHealthChecks($"{MiddlewarePathConstants.HealthCheck}/ready");

        app.MapGet("/", () => Results.Ok(new { Service = "EaaS API", Status = "Running" }));

        app.MapMetrics(MiddlewarePathConstants.Metrics);

        return app;
    }
}
