using System.Globalization;
using EaaS.Api.Constants;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Metrics;
using Microsoft.Extensions.Options;

namespace EaaS.Api.Middleware;

public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRateLimiter rateLimiter, IOptions<RateLimitingSettings> settings)
    {
        // Skip rate limiting for health/metrics endpoints
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith(MiddlewarePathConstants.HealthCheck, StringComparison.Ordinal) || path.StartsWith(MiddlewarePathConstants.Metrics, StringComparison.Ordinal) || path == "/")
        {
            await _next(context);
            return;
        }

        // Extract tenant ID from authenticated claims, fall back to IP
        var tenantId = context.User?.FindFirst(ClaimNameConstants.TenantId)?.Value;
        var rateLimitKey = tenantId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var config = settings.Value;
        var result = await rateLimiter.CheckRateLimitWithInfoAsync(
            rateLimitKey, config.RequestsPerSecond, TimeSpan.FromSeconds(1));

        // Set rate limit headers on all responses
        context.Response.Headers[HttpHeaderConstants.RateLimitLimit] = config.RequestsPerSecond.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers[HttpHeaderConstants.RateLimitRemaining] = Math.Max(0, result.Remaining).ToString(CultureInfo.InvariantCulture);
        context.Response.Headers[HttpHeaderConstants.RateLimitReset] = result.ResetAtUnixMs.ToString(CultureInfo.InvariantCulture);

        if (!result.Allowed)
        {
            EmailMetrics.RateLimitExceeded.WithLabels(tenantId ?? "anonymous").Inc();

            var retryAfterSeconds = Math.Max(1, (result.ResetAtUnixMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
            context.Response.Headers[HttpHeaderConstants.RetryAfter] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "RATE_LIMITED", message = "Too many requests. Please retry later." });
            return;
        }

        await _next(context);
    }
}
