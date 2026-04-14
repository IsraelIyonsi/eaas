namespace EaaS.Application.Email.Providers;

/// <summary>
/// Outcome of a single provider send attempt. Distinct from
/// <see cref="EaaS.Domain.Interfaces.SendEmailResult"/> because this model
/// carries retry semantics required by the provider-abstraction layer.
/// </summary>
public sealed record SendEmailResult
{
    public required bool IsSuccess { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>True when the caller should retry (transient error). Ignored when <see cref="IsSuccess"/>.</summary>
    public bool IsRetryable { get; init; }

    public static SendEmailResult Success(string providerMessageId) =>
        new() { IsSuccess = true, ProviderMessageId = providerMessageId, IsRetryable = false };

    public static SendEmailResult TransientFailure(string errorCode, string errorMessage) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage, IsRetryable = true };

    public static SendEmailResult PermanentFailure(string errorCode, string errorMessage) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage, IsRetryable = false };
}
