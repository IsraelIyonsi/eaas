using EaaS.Domain.Exceptions;
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

            DomainException domainEx => (
                domainEx.StatusCode,
                ApiErrorResponse.Create(domainEx.ErrorCode, domainEx.Message)),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                ApiErrorResponse.Create("UNAUTHORIZED", exception.Message)),

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
