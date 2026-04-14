namespace EaaS.Application.Email.Providers;

/// <summary>
/// Verifies the authenticity of a provider webhook callback.
/// Implementations MUST be constant-time and safe against replay where nonces are supported.
/// </summary>
public interface IWebhookSignatureVerifier
{
    /// <summary>Stable provider identifier; matches the owning <see cref="IEmailProvider.ProviderName"/>.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns true if the payload signature is valid, the timestamp is within the allowed skew,
    /// required headers are present, and (if supported) the nonce has not been seen before.
    /// Implementations MUST NOT throw on malformed input — return false instead.
    /// </summary>
    Task<bool> VerifyAsync(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlyMemory<byte> rawBody,
        CancellationToken cancellationToken = default);
}
