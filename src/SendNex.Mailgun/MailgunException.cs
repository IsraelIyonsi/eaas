namespace SendNex.Mailgun;

/// <summary>
/// Thrown by <see cref="MailgunClient"/> when the Mailgun REST API responds with
/// a terminal (non-retryable) error after resilience policies have exhausted.
/// Callers translate this into a domain-level <c>EmailSendOutcome</c>.
/// </summary>
public sealed class MailgunException : Exception
{
    public int? StatusCode { get; }
    public bool IsRetryable { get; }
    public string? ResponseBody { get; }

    public MailgunException(string message, int? statusCode, bool isRetryable, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        IsRetryable = isRetryable;
        ResponseBody = responseBody;
    }

    public MailgunException(string message, Exception innerException, bool isRetryable)
        : base(message, innerException)
    {
        IsRetryable = isRetryable;
    }
}
