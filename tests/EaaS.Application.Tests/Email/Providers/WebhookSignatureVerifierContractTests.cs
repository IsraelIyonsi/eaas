using EaaS.Application.Email.Providers;
using FluentAssertions;
using Xunit;

namespace EaaS.Application.Tests.Email.Providers;

/// <summary>
/// Provider-agnostic signature verifier contract. Concrete subclasses
/// supply a verifier plus a scenario builder that produces valid/tampered/expired inputs.
/// </summary>
public abstract class WebhookSignatureVerifierContractTests<TVerifier>
    where TVerifier : IWebhookSignatureVerifier
{
    protected abstract TVerifier CreateVerifier();

    /// <summary>Produce a signed payload that the verifier will accept.</summary>
    protected abstract SignedPayload BuildValidPayload();

    /// <summary>Whether this provider supports nonces (enables replay test).</summary>
    protected abstract bool SupportsNonces { get; }

    [Fact]
    public async Task VerifyAsync_WithValidSignature_ReturnsTrue()
    {
        var verifier = CreateVerifier();
        var payload = BuildValidPayload();

        var ok = await verifier.VerifyAsync(payload.Headers, payload.Body);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WithTamperedPayload_ReturnsFalse()
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

        var ok = await verifier.VerifyAsync(payload.Headers, tampered);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_WithExpiredTimestamp_ReturnsFalse()
    {
        var verifier = CreateVerifier();
        var payload = BuildValidPayload();
        var headers = new Dictionary<string, string>(payload.Headers, StringComparer.OrdinalIgnoreCase);

        // Rewind timestamp aggressively beyond any reasonable skew window.
        headers["X-Timestamp"] = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var ok = await verifier.VerifyAsync(headers, payload.Body);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_WithMissingHeaders_ReturnsFalse()
    {
        var verifier = CreateVerifier();
        var payload = BuildValidPayload();

        var ok = await verifier.VerifyAsync(new Dictionary<string, string>(), payload.Body);

        ok.Should().BeFalse();
    }

    [SkippableFact]
    public async Task VerifyAsync_WithReplayedNonce_ReturnsFalse()
    {
        Skip.IfNot(SupportsNonces, "provider does not support nonces");

        var verifier = CreateVerifier();
        var payload = BuildValidPayload();

        var first = await verifier.VerifyAsync(payload.Headers, payload.Body);
        var replay = await verifier.VerifyAsync(payload.Headers, payload.Body);

        first.Should().BeTrue();
        replay.Should().BeFalse("a repeated nonce must be rejected to prevent replay attacks");
    }

    /// <summary>Envelope for a signed test payload.</summary>
    public sealed record SignedPayload(IReadOnlyDictionary<string, string> Headers, ReadOnlyMemory<byte> Body);
}
