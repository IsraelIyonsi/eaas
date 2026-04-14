using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EaaS.WebhookProcessor.Tests.TestSupport;

internal static class ResultExtractor
{
    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
        .BuildServiceProvider();

    /// <summary>
    /// Executes an IResult against a synthetic HttpContext and returns the status code written to
    /// the response. Handles Results.Ok (200), Results.StatusCode(n), Results.BadRequest (400), etc.
    /// </summary>
    internal static async Task<int> ExecuteAsync(this IResult result)
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = Services
        };
        ctx.Response.Body = new MemoryStream();
        await result.ExecuteAsync(ctx);
        return ctx.Response.StatusCode;
    }
}
