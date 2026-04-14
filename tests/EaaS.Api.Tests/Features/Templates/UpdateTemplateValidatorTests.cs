using EaaS.Api.Features.Templates;
using EaaS.Shared.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Templates;

public sealed class UpdateTemplateValidatorTests
{
    private readonly UpdateTemplateValidator _sut = new();

    private static UpdateTemplateCommand Build(
        Guid? tenantId = null,
        Guid? templateId = null,
        string? name = "Updated Template",
        string? subject = "Subject",
        string? html = "<p>Hello</p>",
        string? text = "Hello")
        => new(
            tenantId ?? Guid.NewGuid(),
            templateId ?? Guid.NewGuid(),
            name,
            subject,
            html,
            text);

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        var command = Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_Null()
    {
        var command = Build(name: null);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_HtmlTemplate_Is_Null()
    {
        var command = Build(html: null);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.HtmlTemplate);
    }

    [Fact]
    public void Should_Have_Error_When_TemplateId_Is_Empty()
    {
        var command = Build(templateId: Guid.Empty);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TemplateId)
            .WithErrorMessage("Template ID is required.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_At_Maximum_Length()
    {
        var name = new string('a', EmailConstants.MaxTemplateNameLength);
        var command = Build(name: name);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Exceeds_Maximum_Length()
    {
        var name = new string('a', EmailConstants.MaxTemplateNameLength + 1);
        var command = Build(name: name);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage($"Template name must not exceed {EmailConstants.MaxTemplateNameLength} characters.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_HtmlTemplate_Is_At_Maximum_Size()
    {
        var html = new string('x', 524288);
        var command = Build(html: html);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.HtmlTemplate);
    }

    [Fact]
    public void Should_Have_Error_When_HtmlTemplate_Exceeds_Maximum_Size()
    {
        var html = new string('x', 524289);
        var command = Build(html: html);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.HtmlTemplate)
            .WithErrorMessage("HTML template must not exceed 512KB.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_Empty_String()
    {
        // Name rule only applies when non-null; empty string is allowed.
        var command = Build(name: string.Empty);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
