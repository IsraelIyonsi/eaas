using EaaS.Api.Features.Templates;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Templates;

public sealed class PreviewTemplateValidatorTests
{
    private readonly PreviewTemplateValidator _sut = new();

    private static PreviewTemplateCommand Build(
        Guid? tenantId = null,
        Guid? templateId = null,
        Dictionary<string, object>? variables = null)
        => new(
            tenantId ?? Guid.NewGuid(),
            templateId ?? Guid.NewGuid(),
            variables);

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        var command = Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
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
    public void Should_Not_Have_Error_When_Variables_Is_Null()
    {
        var command = Build(variables: null);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Variables_Is_Empty()
    {
        var command = Build(variables: new Dictionary<string, object>());

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
