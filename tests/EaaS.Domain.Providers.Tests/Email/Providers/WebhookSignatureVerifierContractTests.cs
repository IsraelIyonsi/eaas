using System.Globalization;
using EaaS.Domain.Providers;
using FluentAssertions;
using Xunit;

namespace EaaS.Domain.Providers.Tests.Email.Providers;

/// <summary>
/// Provider-agnostic signature verifier contract. Concrete subclasses
/// supply a verifier plus a scenario builder that produces valid/tampered/expired inputs.
/// The <see cref="GetTimestampHeader"/> hook lets providers plug in their own timestamp
/// header key (SES uses <c>Timestamp</c>, Mailgun uses <c>X-Mailgun-Timestamp</c>, etc.).
/// </summary>
public abstract class WebhookSignatureVerifierContractTests<TVerifier>
    where TVerifier : IWebhookSignatureVerifier
{
    protected abstract TVerifier CreateVerifier();

    /// <summary>Produce a signed payload that the verifier will accept.</summary>
    protected abstract SignedPayload BuildValidPayload();

    /// <summary>
    /// Per-provider timestamp header key. Overridden by adapter-specific subclasses
    /// (SES <c>Timestamp</c>, Mailgun <c>X-Mailgun-Timestamp</c>, etc.). The expired-timestamp
    /// test uses this key to rewind the timestamp beyond the skew window.
    /// </summary>
    protected abstract string GetTimestampHeader();

    /// <summary>Whether this provider supports nonces (gates the replay test).</summary>
    protected abstract bool SupportsNonces { get; }

    [Fact]
    public async Task VerifyAsync_WithValidSignature_ReturnsValid()
    {
        var verifier = CreateVerifier();
        var payload = BuildValidPayload();

        var result = await verifier.VerifyAsync(payload.Body, payload.Headers);

        result.IsValid.Should().BeTrue(
            "baseline signed payload must pass: {0}",
            result.FailureReason ?? "<none>");
    }

    [Fact]
    public async Task VerifyAsync_WithTamperedPayload_ReturnsInvalid()
    {
        var verifier = CreateVerifier();
        var payload = BuildValidPayload();
        var tampered = payload.Body.ToArray();
        if (tampered.Length == 0)
        {
            tampered = new byte[] { 0xFF };
        }
        else
        {
            tampered[0] ^= 0xFF;
        }

        var result = await verifier.VerifyAsync(tampered, payload.Headers);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_WithExpiredTimestamp_ReturnsInvalid()
    {
        var verifier = CreateVerifier();
        var payload = BuildValidPayload();
        var headers = new Dictionary<string, string>(payload.Headers, StringComparer.OrdinalIgnoreCase);

        // Rewind timestamp aggressively beyond any reasonable skew window.
        headers[GetTimestampHeader()] = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var result = await verifier.VerifyAsync(payload.Body, headers);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_WithMissingHeaders_ReturnsInvalid()
    {
        var verifier = CreateVerifier();
        var payload = BuildValidPayload();

        var result = await verifier.VerifyAsync(payload.Body, new Dictionary<string, string>());

        result.IsValid.Should().BeFalse();
    }

    [SkippableFact]
    public async Task VerifyAsync_WithReplayedNonce_ReturnsInvalid()
    {
        Skip.IfNot(SupportsNonces, "provider does not support nonces (EmailProviderCapability.Nonces unset)");

        var verifier = CreateVerifier();
        var payload = BuildValidPayload();

        var first = await verifier.VerifyAsync(payload.Body, payload.Headers);
        var replay = await verifier.VerifyAsync(payload.Body, payload.Headers);

        first.IsValid.Should().BeTrue();
        replay.IsValid.Should().BeFalse("a repeated nonce must be rejected to prevent replay attacks");
    }

    /// <summary>Envelope for a signed test payload.</summary>
    public sealed record SignedPayload(IReadOnlyDictionary<string, string> Headers, ReadOnlyMemory<byte> Body);
}
