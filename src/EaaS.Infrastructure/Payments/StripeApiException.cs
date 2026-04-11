namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Exception thrown when the Stripe API returns an error response.
/// </summary>
public sealed class StripeApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorType { get; }

    public StripeApiException(string message, int statusCode = 0, string? errorType = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
    }

    public StripeApiException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
