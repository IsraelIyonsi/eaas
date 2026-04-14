using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EaaS.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for the RFC 8058 One-Click unsubscribe token service — the
/// stateless HMAC primitive that the Unsubscribe endpoint trusts. These tests
/// pin down the crypto contract: round-trip, tamper rejection, cross-secret
/// rejection, and URL/mailto formatting.
/// </summary>
public sealed class ListUnsubscribeServiceTests
{
    private static ListUnsubscribeService CreateService(string secret = "test-secret-abcdef-01234567")
    {
        var settings = Options.Create(new ListUnsubscribeSettings
        {
            HmacSecret = secret,
            BaseUrl = "https://sendnex.xyz",
            MailtoHost = "sendnex.xyz"
        });
        return new ListUnsubscribeService(settings);
    }

    [Fact]
    public void Should_RoundTrip_ValidToken()
    {
        var sut = CreateService();
        var tenantId = Guid.NewGuid();
        var sentAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

        var token = sut.GenerateToken(tenantId, "jane@example.com", sentAt);
        var decoded = sut.ValidateToken(token);

        decoded.Should().NotBeNull();
        decoded!.TenantId.Should().Be(tenantId);
        decoded.RecipientEmail.Should().Be("jane@example.com");
        decoded.SentAt.Should().BeCloseTo(sentAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_NormalizeEmail_ToLowercase()
    {
        var sut = CreateService();
        var token = sut.GenerateToken(Guid.NewGuid(), "Jane@Example.COM", DateTime.UtcNow);

        var decoded = sut.ValidateToken(token);

        decoded!.RecipientEmail.Should().Be("jane@example.com");
    }

    [Fact]
    public void Should_RejectTamperedToken()
    {
        var sut = CreateService();
        var token = sut.GenerateToken(Guid.NewGuid(), "jane@example.com", DateTime.UtcNow);
        var tampered = token[..^4] + "zzzz";

        sut.ValidateToken(tampered).Should().BeNull();
    }

    [Fact]
    public void Should_RejectToken_SignedWithDifferentSecret()
    {
        var signer = CreateService("secret-A-abcdef-01234567");
        var verifier = CreateService("secret-B-abcdef-01234567");
        var token = signer.GenerateToken(Guid.NewGuid(), "jane@example.com", DateTime.UtcNow);

        verifier.ValidateToken(token).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    [InlineData("!!!not-base64!!!")]
    public void Should_ReturnNull_ForInvalidToken(string token)
    {
        CreateService().ValidateToken(token).Should().BeNull();
    }

    [Fact]
    public void Should_Throw_WhenEmailMissing()
    {
        var sut = CreateService();
        var act = () => sut.GenerateToken(Guid.NewGuid(), "", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("recipientEmail");
    }

    [Fact]
    public void Should_FormatMailtoUnsubscribe()
    {
        var sut = CreateService();
        sut.MailtoUnsubscribe("abc123")
            .Should().Be("mailto:unsubscribe+abc123@sendnex.xyz");
    }

    [Fact]
    public void Should_FormatHttpsUnsubscribe()
    {
        var sut = CreateService();
        sut.HttpsUnsubscribe("abc123")
            .Should().Be("https://sendnex.xyz/u/abc123");
    }

    [Fact]
    public void Should_TrimTrailingSlash_OnBaseUrl()
    {
        var settings = Options.Create(new ListUnsubscribeSettings
        {
            HmacSecret = "any-secret-abcdef-01234567",
            BaseUrl = "https://sendnex.xyz/",
            MailtoHost = "sendnex.xyz"
        });
        var sut = new ListUnsubscribeService(settings);

        sut.HttpsUnsubscribe("xyz").Should().Be("https://sendnex.xyz/u/xyz");
    }

    [Fact]
    public void Should_ProduceDifferentTokens_ForDifferentRecipients()
    {
        var sut = CreateService();
        var tenantId = Guid.NewGuid();
        var sentAt = DateTime.UtcNow;

        var a = sut.GenerateToken(tenantId, "alice@example.com", sentAt);
        var b = sut.GenerateToken(tenantId, "bob@example.com", sentAt);

        a.Should().NotBe(b);
    }
}
