namespace EaaS.Shared.Contracts;

public record ApiErrorResponse(bool Success, ApiError Error)
{
    public static ApiErrorResponse Create(string code, string message, List<ErrorDetail>? details = null)
        => new(false, new ApiError(code, message, details));
}

public record ApiError(string Code, string Message, List<ErrorDetail>? Details = null);

public record ErrorDetail(string Field, string Message);
