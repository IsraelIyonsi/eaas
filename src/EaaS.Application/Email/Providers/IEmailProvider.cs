// NOTE: Minimal scaffold by QA to enable contract test compilation.
// Replace with the Principal Engineer's final design when it lands.
namespace EaaS.Application.Email.Providers;

/// <summary>
/// Provider-agnostic email sender contract. Every concrete provider
/// (SES, Mailgun, SparkPost, Postmark, ...) must satisfy this interface
/// AND pass <see cref="EaaS.Application.Email.Providers"/> contract tests.
/// </summary>
public interface IEmailProvider
{
    /// <summary>Stable, case-insensitive identifier for telemetry / routing (e.g. "ses", "mailgun").</summary>
    string ProviderName { get; }

    /// <summary>Static capability descriptor for this provider.</summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Send a single email. Implementations MUST:
    ///  - throw <see cref="FluentValidation.ValidationException"/> for structurally invalid requests
    ///    (no recipients, null subject, etc.) BEFORE any network I/O.
    ///  - return <see cref="SendEmailResult"/> with <c>IsRetryable = true</c> for transient errors
    ///    (5xx, timeouts, throttling) and <c>IsRetryable = false</c> for 4xx / permanent failures.
    ///  - propagate <see cref="OperationCanceledException"/> when the token is cancelled.
    /// </summary>
    Task<SendEmailResult> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default);
}
