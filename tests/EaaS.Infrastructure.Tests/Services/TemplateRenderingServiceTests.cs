using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EaaS.Infrastructure.Tests.Services;

public sealed class TemplateRenderingServiceTests
{
    private readonly TemplateRenderingService _sut;

    public TemplateRenderingServiceTests()
    {
        var logger = Substitute.For<ILogger<TemplateRenderingService>>();
        _sut = new TemplateRenderingService(logger);
    }

    [Fact]
    public async Task Should_RenderVariables_When_ValidTemplate()
    {
        var variables = new Dictionary<string, object>
        {
            { "name", "Israel" },
            { "company", "EaaS" }
        };

        var result = await _sut.RenderAsync(
            "Welcome {{ name }}",
            "<h1>Hello {{ name }} from {{ company }}</h1>",
            "Hello {{ name }} from {{ company }}",
            variables);

        result.Subject.Should().Be("Welcome Israel");
        result.HtmlBody.Should().Be("<h1>Hello Israel from EaaS</h1>");
        result.TextBody.Should().Be("Hello Israel from EaaS");
    }

    [Fact]
    public async Task Should_RenderSubject_When_SubjectHasVariables()
    {
        var variables = new Dictionary<string, object>
        {
            { "order_id", "12345" }
        };

        var result = await _sut.RenderAsync(
            "Order #{{ order_id }} Confirmed",
            "<p>Your order {{ order_id }} is confirmed.</p>",
            null,
            variables);

        result.Subject.Should().Be("Order #12345 Confirmed");
        result.TextBody.Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowOnSyntaxError()
    {
        var variables = new Dictionary<string, object>();

        var act = () => _sut.RenderAsync(
            "Hello {{ name }",  // Missing closing braces
            "<p>Body</p>",
            null,
            variables);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Template syntax error*");
    }

    [Fact]
    public async Task Should_HandleMissingVariables()
    {
        var variables = new Dictionary<string, object>();

        var result = await _sut.RenderAsync(
            "Hello {{ name }}",
            "<h1>Hello {{ name }}</h1>",
            null,
            variables);

        // Fluid renders missing variables as empty string
        result.Subject.Should().Be("Hello ");
        result.HtmlBody.Should().Be("<h1>Hello </h1>");
    }
}
