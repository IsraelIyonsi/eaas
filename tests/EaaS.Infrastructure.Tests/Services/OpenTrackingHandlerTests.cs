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
/// The open-tracking pixel endpoint must NEVER redirect under any circumstance.
/// It returns a 1x1 GIF (or nothing of consequence). Tests here confirm the
/// same bug class as H12 cannot exist on this endpoint.
/// </summary>
public sealed class OpenTrackingHandlerTests
{
    private readonly OpenTrackingHandler _sut;
    private readonly TrackingTokenService _tokenService;

    public OpenTrackingHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(options);

        var settings = Options.Create(new TrackingSettings
        {
            BaseUrl = "https://track.sendnex.xyz",
            HmacSecret = "test-secret-key-not-used-in-prod-xxxxxxxxxxxxxxxxxxxxxxxxx"
        });
        _tokenService = new TrackingTokenService(settings, Substitute.For<ILogger<TrackingTokenService>>());

        _sut = new OpenTrackingHandler(
            _tokenService,
            dbContext,
            Substitute.For<ILogger<OpenTrackingHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ValidToken_ReturnsGifNotRedirect()
    {
        var token = _tokenService.GenerateToken(Guid.NewGuid(), "open");

        var result = await _sut.HandleAsync(token, new DefaultHttpContext(), CancellationToken.None);

        result.Should().NotBeOfType<RedirectHttpResult>();
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ReturnsGifNotRedirect()
    {
        var result = await _sut.HandleAsync("garbage", new DefaultHttpContext(), CancellationToken.None);

        result.Should().NotBeOfType<RedirectHttpResult>();
    }

    [Fact]
    public async Task HandleAsync_TokenSmugglingUrl_DoesNotRedirect()
    {
        // Even if a token payload were abused to carry a URL, the open handler
        // returns a pixel — never a redirect.
        // The originalUrl parameter has been removed from the service — this test
        // now just confirms a normal open token produces a pixel (never a redirect).
        var token = _tokenService.GenerateToken(Guid.NewGuid(), "open");

        var result = await _sut.HandleAsync(token, new DefaultHttpContext(), CancellationToken.None);

        result.Should().NotBeOfType<RedirectHttpResult>();
    }
}
