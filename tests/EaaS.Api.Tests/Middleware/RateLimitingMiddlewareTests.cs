using System.Globalization;
using System.Security.Claims;
using EaaS.Api.Middleware;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Middleware;

public sealed class RateLimitingMiddlewareTests
{
    private readonly IRateLimiter _rateLimiter;
    private readonly IOptions<RateLimitingSettings> _settings;
    private readonly RateLimitingMiddleware _sut;
    private readonly long _futureResetMs;

    public RateLimitingMiddlewareTests()
    {
        _rateLimiter = Substitute.For<IRateLimiter>();
        _settings = Options.Create(new RateLimitingSettings { RequestsPerSecond = 10, BurstSize = 20 });
        _futureResetMs = DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeMilliseconds();

        // Default: allow the request
        _rateLimiter
            .CheckRateLimitWithInfoAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 9, _futureResetMs));

        _sut = new RateLimitingMiddleware(_ => Task.CompletedTask);
    }

    [Fact]
    public async Task Should_AllowRequest_When_UnderRateLimit()
    {
        var context = CreateHttpContext();

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Should_Return429_When_RateLimitExceeded()
    {
        _rateLimiter
            .CheckRateLimitWithInfoAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(false, 0, _futureResetMs));

        var context = CreateHttpContext();

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Should_SetXRateLimitLimitHeader_OnAllowedRequest()
    {
        var context = CreateHttpContext();

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
    }

    [Fact]
    public async Task Should_SetXRateLimitRemainingHeader_OnAllowedRequest()
    {
        var context = CreateHttpContext();

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("9");
    }

    [Fact]
    public async Task Should_SetRetryAfterHeader_When_RateLimitExceeded()
    {
        var resetMs = DateTimeOffset.UtcNow.AddSeconds(2).ToUnixTimeMilliseconds();

        _rateLimiter
            .CheckRateLimitWithInfoAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(false, 0, resetMs));

        var context = CreateHttpContext();

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        context.Response.Headers["Retry-After"].ToString().Should().NotBeNullOrEmpty();
        int.Parse(context.Response.Headers["Retry-After"].ToString(), CultureInfo.InvariantCulture).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Should_UseTenantId_AsRateLimitKey_ForAuthenticatedRequest()
    {
        var tenantId = "tenant-abc-123";
        var context = CreateHttpContextWithTenant(tenantId);

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        await _rateLimiter.Received(1)
            .CheckRateLimitWithInfoAsync(tenantId, Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_UseIpAddress_AsRateLimitKey_ForUnauthenticatedRequest()
    {
        var context = CreateHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        await _rateLimiter.Received(1)
            .CheckRateLimitWithInfoAsync("192.168.1.1", Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/metrics")]
    [InlineData("/")]
    public async Task Should_SkipRateLimiting_ForExemptPaths(string path)
    {
        var context = CreateHttpContext(path);

        await _sut.InvokeAsync(context, _rateLimiter, _settings);

        await _rateLimiter.DidNotReceive()
            .CheckRateLimitWithInfoAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private static DefaultHttpContext CreateHttpContext(string path = "/api/emails")
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;
        return context;
    }

    private static DefaultHttpContext CreateHttpContextWithTenant(string tenantId, string path = "/api/emails")
    {
        var context = CreateHttpContext(path);
        var claims = new[] { new Claim("TenantId", tenantId) };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return context;
    }
}
