namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Exception thrown when the PayStack API returns an error response.
/// </summary>
public sealed class PayStackApiException : Exception
{
    public int StatusCode { get; }

    public PayStackApiException(string message, int statusCode = 0)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public PayStackApiException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
