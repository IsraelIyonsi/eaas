using System.Text;
using EaaS.Infrastructure.EmailProviders.Providers.Mailgun;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using SendNex.Mailgun;
using Xunit;

namespace EaaS.Infrastructure.Tests.EmailProviders.Mailgun;

public sealed class MailgunWebhookSignatureVerifierTests
{
    private static readonly Dictionary<string, string> NoHeaders = new();

    [Fact]
    public async Task VerifyAsync_ValidSignature_ReturnsIsValidTrue()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var ts = time.GetUtcNow().ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var token = "token-abc";
        var sig = MailgunTestFixtures.Hmac(MailgunTestFixtures.SigningKey, ts + token);

        var payload = BuildPayload(ts, token, sig);
        var verifier = new MailgunWebhookSignatureVerifier(
            MailgunTestFixtures.Options(),
            time,
            MailgunTestFixtures.Logger<MailgunWebhookSignatureVerifier>());

        var result = await verifier.VerifyAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);

        result.IsValid.Should().BeTrue();
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task VerifyAsync_TamperedSignature_Rejects()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var ts = MailgunTestFixtures.UnixNow(time);
        var token = "token-abc";
        var tamperedSig = new string('0', 64);

        var payload = BuildPayload(ts, token, tamperedSig);
        var verifier = new MailgunWebhookSignatureVerifier(
            MailgunTestFixtures.Options(),
            time,
            MailgunTestFixtures.Logger<MailgunWebhookSignatureVerifier>());

        var result = await verifier.VerifyAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be("Signature mismatch.");
    }

    [Fact]
    public async Task VerifyAsync_StaleTimestamp_Rejects()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var staleTs = time.GetUtcNow().AddMinutes(-10).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var token = "token-abc";
        var sig = MailgunTestFixtures.Hmac(MailgunTestFixtures.SigningKey, staleTs + token);

        var payload = BuildPayload(staleTs, token, sig);
        var verifier = new MailgunWebhookSignatureVerifier(
            MailgunTestFixtures.Options(),
            time,
            MailgunTestFixtures.Logger<MailgunWebhookSignatureVerifier>());

        var result = await verifier.VerifyAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be("Timestamp outside replay tolerance.");
    }

    [Fact]
    public async Task VerifyAsync_MissingSignatureBlock_Rejects()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var payload = """{"event-data":{"event":"delivered"}}""";

        var verifier = new MailgunWebhookSignatureVerifier(
            MailgunTestFixtures.Options(),
            time,
            MailgunTestFixtures.Logger<MailgunWebhookSignatureVerifier>());

        var result = await verifier.VerifyAsync(Encoding.UTF8.GetBytes(payload), NoHeaders);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Contain("Missing");
    }

    [Fact]
    public async Task VerifyAsync_MalformedJson_Rejects()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var verifier = new MailgunWebhookSignatureVerifier(
            MailgunTestFixtures.Options(),
            time,
            MailgunTestFixtures.Logger<MailgunWebhookSignatureVerifier>());

        var result = await verifier.VerifyAsync(Encoding.UTF8.GetBytes("{not json"), NoHeaders);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Contain("Malformed");
    }

    [Fact]
    public void GetTimestampHeader_ReturnsMailgunTimestampField()
    {
        MailgunWebhookSignatureVerifier.GetTimestampHeader()
            .Should().Be(MailgunConstants.Webhook.Timestamp);
    }

    private static string BuildPayload(string timestamp, string token, string signature) =>
        $$"""
        {
          "signature": { "timestamp": "{{timestamp}}", "token": "{{token}}", "signature": "{{signature}}" },
          "event-data": { "event": "delivered" }
        }
        """;
}
