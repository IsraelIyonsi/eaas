using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.ApiKeys;

public sealed class CreateApiKeyValidatorTests
{
    private readonly CreateApiKeyValidator _sut = new();

    [Fact]
    public void Should_Pass_When_NameProvided()
    {
        var command = TestDataBuilders.CreateApiKey().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_NameEmpty()
    {
        var command = TestDataBuilders.CreateApiKey()
            .WithName(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void Should_Fail_When_NameTooLong()
    {
        var command = TestDataBuilders.CreateApiKey()
            .WithName(new string('A', 101))
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not exceed 100 characters.");
    }
}
