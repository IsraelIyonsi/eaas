using System.Text.Json;
using EaaS.Api.Middleware;
using EaaS.Shared.Contracts;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Api.Tests.Middleware;

public sealed class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _sut;

    public GlobalExceptionHandlerTests()
    {
        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        _sut = new GlobalExceptionHandler(logger);
    }

    [Fact]
    public async Task Should_Return400_When_ValidationException()
    {
        var httpContext = CreateHttpContext();
        var failures = new List<ValidationFailure>
        {
            new("From", "From address is required.")
        };
        var exception = new ValidationException(failures);

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Should_Return404_When_KeyNotFoundException()
    {
        var httpContext = CreateHttpContext();
        var exception = new KeyNotFoundException("Email not found.");

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Should_Return500_When_UnhandledException()
    {
        var httpContext = CreateHttpContext();
        var exception = new NullReferenceException("Something went wrong");

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
