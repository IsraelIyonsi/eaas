using EaaS.Api.Features.Suppressions;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Suppressions;

public sealed class AddSuppressionValidatorTests
{
    private readonly AddSuppressionValidator _sut = new();

    [Fact]
    public void Should_Pass_When_ValidEmailAddress()
    {
        var command = TestDataBuilders.AddSuppression().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_EmailAddressEmpty()
    {
        var command = TestDataBuilders.AddSuppression()
            .WithEmailAddress(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EmailAddress)
            .WithErrorMessage("Email address is required.");
    }

    [Fact]
    public void Should_Fail_When_EmailAddressInvalid()
    {
        var command = TestDataBuilders.AddSuppression()
            .WithEmailAddress("not-an-email")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EmailAddress)
            .WithErrorMessage("Must be a valid email address.");
    }
}
