using EaaS.Api.Features.PasswordReset;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.PasswordReset;

public sealed class ForgotPasswordValidatorTests
{
    private readonly ForgotPasswordValidator _sut = new();

    [Fact]
    public void Should_Pass_WhenEmailValid()
    {
        var result = _sut.TestValidate(new ForgotPasswordCommand("user@example.com", null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenEmailEmpty()
    {
        var result = _sut.TestValidate(new ForgotPasswordCommand("", null));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Fail_WhenEmailInvalid()
    {
        var result = _sut.TestValidate(new ForgotPasswordCommand("not-an-email", null));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Pass_WithUppercaseEmail()
    {
        var result = _sut.TestValidate(new ForgotPasswordCommand("USER@EXAMPLE.COM", null));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
