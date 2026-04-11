namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Exception thrown when the Flutterwave API returns an error response.
/// </summary>
public sealed class FlutterwaveApiException : Exception
{
    public int StatusCode { get; }

    public FlutterwaveApiException(string message, int statusCode = 0)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public FlutterwaveApiException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
