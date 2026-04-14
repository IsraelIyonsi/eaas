using System.Text;
using System.Text.Json;
using EaaS.WebhookProcessor.Models;
using Microsoft.AspNetCore.Http;

namespace EaaS.WebhookProcessor.Tests.TestSupport;

internal static class FakeHttpRequest
{
    internal static HttpRequest FromJson(SnsMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        return FromBody(json);
    }

    internal static HttpRequest FromBody(string body)
    {
        var ctx = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.LongLength;
        ctx.Request.ContentType = "application/json";
        ctx.Request.Method = "POST";
        return ctx.Request;
    }
}
