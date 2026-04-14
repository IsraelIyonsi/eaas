namespace EaaS.Domain.Providers;

public readonly record struct WebhookVerificationResult(
    bool IsValid,
    string? FailureReason);

/// <summary>
/// Per-provider webhook signature verifier. Signature verification is mandatory for
/// every adapter — there is no config key, constructor argument, or feature flag that
/// disables it outside an audited operational kill-switch. See architect doc §3.5 / §6.3.
/// </summary>
public interface IWebhookSignatureVerifier
{
    string ProviderKey { get; }

    Task<WebhookVerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
