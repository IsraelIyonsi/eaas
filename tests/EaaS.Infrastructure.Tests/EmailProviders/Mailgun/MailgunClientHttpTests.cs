using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SendNex.Mailgun;
using SendNex.Mailgun.Dtos;
using Xunit;

namespace EaaS.Infrastructure.Tests.EmailProviders.Mailgun;

/// <summary>
/// Wire-level checks against <see cref="MailgunClient"/> using a stub
/// <see cref="HttpMessageHandler"/>. These lock down the exact on-the-wire
/// shape (path, form fields, <c>v:tenant_id</c> presence) that Mailgun sees.
/// </summary>
public sealed class MailgunClientHttpTests
{
    [Fact]
    public async Task SendAsync_FormBodyContainsTenantIdCustomVariable()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK,
            """{ "id": "<msg-1@mg.example.com>", "message": "Queued." }""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.net") };
        var sut = new MailgunClient(http, NullLogger<MailgunClient>.Instance);

        var req = new MailgunSendRequest
        {
            Domain = "mg.example.com",
            From = "Sender <s@example.com>",
            To = new[] { "user@example.com" },
            Subject = "Hi",
            Text = "body",
            CustomVariables = new Dictionary<string, string> { ["tenant_id"] = "tnt-1" }
        };

        var response = await sut.SendAsync(req);
        response.Id.Should().Be("<msg-1@mg.example.com>");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/v3/mg.example.com/messages");
        handler.LastRequestBody.Should().Contain("v:tenant_id");
        handler.LastRequestBody.Should().Contain("tnt-1");
        handler.LastRequestBody.Should().MatchRegex("name=\"?to\"?");
        handler.LastRequestBody.Should().Contain("user@example.com");
    }

    [Fact]
    public async Task SendAsync_5xx_ThrowsRetryableMailgunException()
    {
        var handler = new CapturingHandler(HttpStatusCode.ServiceUnavailable, "mailgun down");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.net") };
        var sut = new MailgunClient(http, NullLogger<MailgunClient>.Instance);

        var req = new MailgunSendRequest
        {
            Domain = "mg.example.com",
            From = "s@example.com",
            To = new[] { "u@example.com" },
            Subject = "Hi",
            Text = "body"
        };

        var ex = await Assert.ThrowsAsync<MailgunException>(() => sut.SendAsync(req));
        ex.StatusCode.Should().Be(503);
        ex.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_400_ThrowsNonRetryable()
    {
        var handler = new CapturingHandler(HttpStatusCode.BadRequest, """{"message":"bad"}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.net") };
        var sut = new MailgunClient(http, NullLogger<MailgunClient>.Instance);

        var req = new MailgunSendRequest
        {
            Domain = "mg.example.com",
            From = "s@example.com",
            To = new[] { "u@example.com" },
            Subject = "Hi",
            Text = "body"
        };

        var ex = await Assert.ThrowsAsync<MailgunException>(() => sut.SendAsync(req));
        ex.StatusCode.Should().Be(400);
        ex.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public async Task SendRawAsync_PostsToMessagesMimeEndpoint()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK,
            """{ "id": "<raw@mg.example.com>", "message": "Queued." }""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.net") };
        var sut = new MailgunClient(http, NullLogger<MailgunClient>.Instance);

        using var mime = new MemoryStream(Encoding.UTF8.GetBytes("From: s@example.com\r\n\r\nhello"));
        var vars = new Dictionary<string, string> { ["tenant_id"] = "tnt-2" };

        var response = await sut.SendRawAsync("mg.example.com", mime, vars);
        response.Id.Should().Be("<raw@mg.example.com>");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/v3/mg.example.com/messages.mime");
        handler.LastRequestBody.Should().Contain("v:tenant_id");
        handler.LastRequestBody.Should().Contain("tnt-2");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        public CapturingHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
