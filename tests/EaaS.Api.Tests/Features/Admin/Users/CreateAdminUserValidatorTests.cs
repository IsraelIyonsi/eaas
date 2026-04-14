using EaaS.Api.Features.Admin.Users;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Users;

public sealed class CreateAdminUserValidatorTests
{
    private readonly CreateAdminUserValidator _sut = new();

    [Fact]
    public void Should_Pass_WhenValid()
    {
        var command = TestDataBuilders.CreateAdminUser().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenEmailEmpty()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithEmail(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordTooShort()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithPassword("Abc1!xyz") // 8 chars — below admin 12-char minimum
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 12 characters.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingUppercase()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithPassword("nouppercase1!xyz")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingLowercase()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithPassword("NOLOWERCASE1!XYZ")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingDigit()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithPassword("NoDigitsHere!xyz")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingSymbol()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithPassword("NoSymbols123abcd")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one symbol.");
    }

    [Fact]
    public void Should_Pass_WhenPasswordIsStrong()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithPassword("StrongPassw0rd!")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_Fail_WhenRoleInvalid()
    {
        var command = TestDataBuilders.CreateAdminUser()
            .WithRole("InvalidRole")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Role);
    }
}
