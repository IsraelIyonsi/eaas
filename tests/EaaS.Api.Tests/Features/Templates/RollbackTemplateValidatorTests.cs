using EaaS.Api.Features.Templates;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Templates;

public sealed class RollbackTemplateValidatorTests
{
    private readonly RollbackTemplateValidator _sut = new();

    private static RollbackTemplateCommand Build(
        Guid? tenantId = null,
        Guid? templateId = null,
        int targetVersion = 1)
        => new(
            tenantId ?? Guid.NewGuid(),
            templateId ?? Guid.NewGuid(),
            targetVersion);

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

        result.ShouldHaveValidationErrorFor(x => x.TemplateId);
    }

    [Fact]
    public void Should_Have_Error_When_TenantId_Is_Empty()
    {
        var command = Build(tenantId: Guid.Empty);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Should_Have_Error_When_TargetVersion_Is_Not_Positive(int version)
    {
        var command = Build(targetVersion: version);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TargetVersion)
            .WithErrorMessage("Version must be a positive integer.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void Should_Not_Have_Error_When_TargetVersion_Is_Positive(int version)
    {
        var command = Build(targetVersion: version);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TargetVersion);
    }
}
