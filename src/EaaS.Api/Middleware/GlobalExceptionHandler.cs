using EaaS.Shared.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace EaaS.Api.Middleware;

public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, errorResponse) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                ApiErrorResponse.Create(
                    "VALIDATION_ERROR",
                    "One or more validation errors occurred.",
                    validationEx.Errors
                        .Select(e => new ErrorDetail(e.PropertyName, e.ErrorMessage))
                        .ToList())),

            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                ApiErrorResponse.Create("NOT_FOUND", exception.Message)),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                ApiErrorResponse.Create("UNAUTHORIZED", exception.Message)),

            InvalidOperationException when exception.Message.Contains("already exists", StringComparison.Ordinal) => (
                StatusCodes.Status409Conflict,
                ApiErrorResponse.Create("CONFLICT", exception.Message)),

            InvalidOperationException when exception.Message.Contains("not verified", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status422UnprocessableEntity,
                ApiErrorResponse.Create("DOMAIN_NOT_VERIFIED", exception.Message)),

            InvalidOperationException when exception.Message.Contains("suppression list", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status422UnprocessableEntity,
                ApiErrorResponse.Create("RECIPIENT_SUPPRESSED", exception.Message)),

            InvalidOperationException when exception.Message.Contains("Rate limit", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status429TooManyRequests,
                ApiErrorResponse.Create("RATE_LIMIT_EXCEEDED", exception.Message)),

            _ => (
                StatusCodes.Status500InternalServerError,
                ApiErrorResponse.Create("INTERNAL_ERROR", "An unexpected error occurred."))
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(_logger, exception);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception occurred")]
    private static partial void LogUnhandledException(ILogger logger, Exception ex);
}
