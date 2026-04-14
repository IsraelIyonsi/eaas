namespace EaaS.Domain.Providers;

/// <summary>
/// Outcome of a provider send call. Named <c>EmailSendOutcome</c> (not <c>SendEmailResult</c>)
/// to avoid colliding with the legacy <see cref="EaaS.Domain.Interfaces.SendEmailResult"/>
/// record; the legacy record remains for the time being on other (non-send) paths.
/// </summary>
public sealed record EmailSendOutcome(
    bool Success,
    string? ProviderMessageId,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsRetryable);
