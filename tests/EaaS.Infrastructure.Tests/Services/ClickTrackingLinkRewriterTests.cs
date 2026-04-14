using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EaaS.Infrastructure.Tests.Services;

/// <summary>
/// H12 rev-2 scheme allowlist: RewriteLinksAsync must only persist and rewrite
/// http(s) absolute URLs. Dangerous schemes (javascript:, data:, file:, vbscript:)
/// and non-web schemes (mailto:) must be left untouched — never laundered through
/// our click endpoint, never persisted to TrackingLinks.OriginalUrl.
/// </summary>
public sealed class ClickTrackingLinkRewriterTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ClickTrackingLinkRewriter _sut;

    public ClickTrackingLinkRewriterTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        var settings = Options.Create(new TrackingSettings
        {
            BaseUrl = "https://track.sendnex.xyz",
            HmacSecret = "test-secret-key-not-used-in-prod-xxxxxxxxxxxxxxxxxxxxxxxxx"
        });

        _sut = new ClickTrackingLinkRewriter(
            _dbContext,
            settings,
            Substitute.For<ILogger<ClickTrackingLinkRewriter>>());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    public async Task RewriteLinksAsync_DisallowedScheme_LeavesHrefUntouched(string dangerousHref)
    {
        var html = $"<html><body><a href=\"{dangerousHref}\">click</a></body></html>";

        var result = await _sut.RewriteLinksAsync(html, Guid.NewGuid(), CancellationToken.None);

        result.Should().Contain(dangerousHref);
        result.Should().NotContain("/track/click/");
        _dbContext.TrackingLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task RewriteLinksAsync_MailtoScheme_LeavesHrefUntouched()
    {
        // mailto: is filtered out earlier by the explicit skip rule, but we assert
        // end-state behavior: it must not be rewritten and must not be persisted.
        var html = "<html><body><a href=\"mailto:foo@bar.com\">email</a></body></html>";

        var result = await _sut.RewriteLinksAsync(html, Guid.NewGuid(), CancellationToken.None);

        result.Should().Contain("mailto:foo@bar.com");
        result.Should().NotContain("/track/click/");
        _dbContext.TrackingLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task RewriteLinksAsync_HttpsScheme_IsRewritten()
    {
        var html = "<html><body><a href=\"https://example.com/path\">go</a></body></html>";
        var emailId = Guid.NewGuid();

        var result = await _sut.RewriteLinksAsync(html, emailId, CancellationToken.None);

        result.Should().Contain("/track/click/");
        result.Should().NotContain("href=\"https://example.com/path\"");

        var link = _dbContext.TrackingLinks.Should().ContainSingle().Subject;
        link.OriginalUrl.Should().Be("https://example.com/path");
        link.EmailId.Should().Be(emailId);
    }

    [Fact]
    public async Task RewriteLinksAsync_HttpScheme_IsRewritten()
    {
        var html = "<html><body><a href=\"http://example.com/\">go</a></body></html>";

        var result = await _sut.RewriteLinksAsync(html, Guid.NewGuid(), CancellationToken.None);

        result.Should().Contain("/track/click/");
        _dbContext.TrackingLinks.Should().ContainSingle()
            .Which.OriginalUrl.Should().Be("http://example.com/");
    }

    [Fact]
    public async Task RewriteLinksAsync_RelativeUrl_LeavesHrefUntouched()
    {
        // Relative URLs fail Uri.TryCreate(..., Absolute, ...) and must be skipped.
        var html = "<html><body><a href=\"/relative/path\">go</a></body></html>";

        var result = await _sut.RewriteLinksAsync(html, Guid.NewGuid(), CancellationToken.None);

        result.Should().Contain("/relative/path");
        result.Should().NotContain("/track/click/");
        _dbContext.TrackingLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task RewriteLinksAsync_MixedSchemes_RewritesOnlyHttpS()
    {
        var html = """
            <html><body>
              <a href="https://good.com">good</a>
              <a href="javascript:alert(1)">bad</a>
              <a href="http://also-good.com">also good</a>
              <a href="data:text/html,x">bad</a>
            </body></html>
            """;

        var result = await _sut.RewriteLinksAsync(html, Guid.NewGuid(), CancellationToken.None);

        result.Should().Contain("javascript:alert(1)");
        result.Should().Contain("data:text/html,x");
        _dbContext.TrackingLinks.Should().HaveCount(2);
        _dbContext.TrackingLinks.Select(l => l.OriginalUrl).Should()
            .BeEquivalentTo(ExpectedRewrittenUrls);
    }

    private static readonly string[] ExpectedRewrittenUrls =
        { "https://good.com", "http://also-good.com" };

    public void Dispose() => _dbContext.Dispose();
}
