using System.Security.Cryptography;
using System.Text;
using EaaS.Domain.Entities;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using EaaS.WebhookProcessor.Handlers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EaaS.Infrastructure.Tests.Services;

/// <summary>
/// Security regression tests for H12 (open redirect via click tracker).
/// The click endpoint must only redirect to URLs persisted in the TrackingLinks
/// table at send time — never to a URL supplied via the request (query string
/// or HMAC payload). This is enforced regardless of HMAC validity.
/// </summary>
public sealed class ClickTrackingHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly TrackingTokenService _tokenService;
    private readonly ClickTrackingHandler _sut;

    public ClickTrackingHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        var settings = Options.Create(new TrackingSettings
        {
            BaseUrl = "https://track.sendnex.xyz",
            HmacSecret = HmacSecret
        });
        _tokenService = new TrackingTokenService(settings, Substitute.For<ILogger<TrackingTokenService>>());

        _sut = new ClickTrackingHandler(
            _tokenService,
            _dbContext,
            Substitute.For<ILogger<ClickTrackingHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ValidStoredToken_RedirectsToOriginalUrl()
    {
        var emailId = Guid.NewGuid();
        var link = new TrackingLink
        {
            Id = Guid.NewGuid(),
            EmailId = emailId,
            Token = "abc123token",
            OriginalUrl = "https://example.com/legit",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.TrackingLinks.Add(link);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.HandleAsync(link.Token, CreateHttpContext(), CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectHttpResult>().Subject;
        redirect.Url.Should().Be("https://example.com/legit");
    }

    [Fact]
    public async Task HandleAsync_UnknownToken_Returns404()
    {
        var result = await _sut.HandleAsync("not-a-real-token", CreateHttpContext(), CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task HandleAsync_EmptyToken_Returns404()
    {
        var result = await _sut.HandleAsync(string.Empty, CreateHttpContext(), CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    /// <summary>
    /// H12 regression: even a correctly HMAC-signed token carrying an attacker URL
    /// must NOT trigger a redirect. The endpoint must only honor DB-stored links.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ValidHmacTokenWithAttackerUrl_DoesNotRedirect()
    {
        // Attacker obtains the HMAC key (e.g. key leak) and forges a token whose
        // payload embeds an attacker-controlled URL. The service no longer accepts
        // originalUrl — we build the raw tampered payload directly to prove the
        // handler ignores any URL smuggled into the token and only trusts DB rows.
        var maliciousToken = ForgeTokenWithExtraFields(
            HmacSecret,
            emailId: Guid.NewGuid(),
            eventType: "click",
            attackerUrl: "https://evil.com/phish");

        var result = await _sut.HandleAsync(maliciousToken, CreateHttpContext(), CancellationToken.None);

        // Must NOT redirect — no DB row exists for this token.
        result.Should().BeOfType<NotFound>();
        result.Should().NotBeOfType<RedirectHttpResult>();
    }

    /// <summary>
    /// Builds a raw HMAC-signed tracking token with an attacker-controlled
    /// "originalUrl" field smuggled into the JSON payload. Mirrors the wire
    /// format used by TrackingTokenService.GenerateToken so a valid signature
    /// is produced. Used only to prove the handler ignores payload URLs.
    /// </summary>
    private static string ForgeTokenWithExtraFields(string hmacSecret, Guid emailId, string eventType, string attackerUrl)
    {
        var payloadJson = $"{{\"EmailId\":\"{emailId}\",\"EventType\":\"{eventType}\",\"OriginalUrl\":\"{attackerUrl}\"}}";
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacSecret));
        var signature = hmac.ComputeHash(payloadBytes);

        var combined = new byte[4 + signature.Length + payloadBytes.Length];
        BitConverter.GetBytes(signature.Length).CopyTo(combined, 0);
        signature.CopyTo(combined, 4);
        payloadBytes.CopyTo(combined, 4 + signature.Length);

        return Convert.ToBase64String(combined)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private const string HmacSecret = "test-secret-key-not-used-in-prod-xxxxxxxxxxxxxxxxxxxxxxxxx";

    /// <summary>
    /// H12 regression: tokens with tampered/invalid HMAC must not redirect.
    /// </summary>
    [Fact]
    public async Task HandleAsync_TamperedToken_Returns404()
    {
        var tampered = "XXXtamperedYYYnotInDbZZZ";

        var result = await _sut.HandleAsync(tampered, CreateHttpContext(), CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    /// <summary>
    /// H12 regression: a querystring `?url=` parameter (the classic open-redirect
    /// vector) must have zero effect — the handler signature does not accept it.
    /// </summary>
    [Fact]
    public async Task HandleAsync_QueryStringUrlParam_Ignored()
    {
        var emailId = Guid.NewGuid();
        var link = new TrackingLink
        {
            Id = Guid.NewGuid(),
            EmailId = emailId,
            Token = "legit-token",
            OriginalUrl = "https://example.com/legit",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.TrackingLinks.Add(link);
        await _dbContext.SaveChangesAsync();

        var ctx = CreateHttpContext();
        ctx.Request.QueryString = new QueryString("?url=https://evil.com/phish");

        var result = await _sut.HandleAsync(link.Token, ctx, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectHttpResult>().Subject;
        redirect.Url.Should().Be("https://example.com/legit");
        redirect.Url.Should().NotContain("evil.com");
    }

    public void Dispose() => _dbContext.Dispose();

    private static DefaultHttpContext CreateHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.UserAgent = "xunit-test";
        return ctx;
    }
}
