using EaaS.Api.Constants;
using Serilog.Context;

namespace EaaS.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = HttpHeaderConstants.CorrelationId;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        context.Items[ContextItemConstants.CorrelationId] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
