using System.Text.Json;
using EaaS.Api.Middleware;
using EaaS.Domain.Exceptions;
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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
        var exception = new FluentValidation.ValidationException(failures);

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // BUG-H2: A FluentValidation failure from a pipeline validator (e.g. webhook URL
    // syntactically invalid) must surface as HTTP 400 with code=VALIDATION_ERROR and
    // every field-level failure serialised under details[], not leak as a 500.
    [Fact]
    public async Task Should_Return400WithFieldErrors_When_FluentValidationExceptionMultiField()
    {
        var httpContext = CreateHttpContext();
        var failures = new List<ValidationFailure>
        {
            new("Url", "URL must be a valid HTTPS URL."),
            new("Events", "At least one event type is required.")
        };
        var exception = new FluentValidation.ValidationException(failures);

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        var payload = JsonSerializer.Deserialize<ApiErrorResponse>(body, JsonOptions);

        payload.Should().NotBeNull();
        payload!.Error.Code.Should().Be("VALIDATION_ERROR");
        payload.Error.Details.Should().NotBeNull();
        payload.Error.Details!.Should().HaveCount(2);
        payload.Error.Details.Should().Contain(d => d.Field == "Url");
        payload.Error.Details.Should().Contain(d => d.Field == "Events");
    }

    [Fact]
    public async Task Should_Return404_When_NotFoundException()
    {
        var httpContext = CreateHttpContext();
        var exception = new NotFoundException("Email not found.");

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Should_Return429_When_RateLimitExceededException()
    {
        var httpContext = CreateHttpContext();
        var exception = new RateLimitExceededException("Rate limit exceeded.");

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Should_Return409_When_ConflictException()
    {
        var httpContext = CreateHttpContext();
        var exception = new ConflictException("Resource already exists.");

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Should_Return422_When_DomainNotVerifiedException()
    {
        var httpContext = CreateHttpContext();
        var exception = new DomainNotVerifiedException("Domain 'example.com' is not verified.");

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Should_Return422_When_RecipientSuppressedException()
    {
        var httpContext = CreateHttpContext();
        var exception = new RecipientSuppressedException("Recipient is on the suppression list.");

        var handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
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
