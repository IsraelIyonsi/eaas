using EaaS.Api.Features.PasswordReset;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.PasswordReset;

public sealed class ResetPasswordValidatorTests
{
    private readonly ResetPasswordValidator _sut = new();

    [Fact]
    public void Should_Pass_WhenValid()
    {
        var result = _sut.TestValidate(new ResetPasswordCommand("valid-token", "NewPass123"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenTokenEmpty()
    {
        var result = _sut.TestValidate(new ResetPasswordCommand("", "NewPass123"));
        result.ShouldHaveValidationErrorFor(x => x.Token);
    }

    [Fact]
    public void Should_Fail_WhenPasswordTooShort()
    {
        var result = _sut.TestValidate(new ResetPasswordCommand("tok", "Abc1"));
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingDigit()
    {
        var result = _sut.TestValidate(new ResetPasswordCommand("tok", "OnlyLetters"));
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingLetter()
    {
        var result = _sut.TestValidate(new ResetPasswordCommand("tok", "12345678"));
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void Should_Fail_WhenPasswordEmpty()
    {
        var result = _sut.TestValidate(new ResetPasswordCommand("tok", ""));
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}
