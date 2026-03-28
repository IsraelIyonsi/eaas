using EaaS.Api.Features.Templates;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Templates;

public sealed class CreateTemplateValidatorTests
{
    private readonly CreateTemplateValidator _sut = new();

    [Fact]
    public void Should_Pass_When_Valid()
    {
        var command = TestDataBuilders.CreateTemplate().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_NameEmpty()
    {
        var command = TestDataBuilders.CreateTemplate()
            .WithName(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Template name is required.");
    }

    [Fact]
    public void Should_Fail_When_HtmlBodyEmpty()
    {
        var command = TestDataBuilders.CreateTemplate()
            .WithHtmlBody(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.HtmlBody)
            .WithErrorMessage("HTML body is required.");
    }
}
