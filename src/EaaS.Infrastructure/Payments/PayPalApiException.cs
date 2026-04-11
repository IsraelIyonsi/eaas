namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Exception thrown when the PayPal API returns an error response.
/// </summary>
public sealed class PayPalApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorType { get; }

    public PayPalApiException(string message, int statusCode = 0, string? errorType = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
    }

    public PayPalApiException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
