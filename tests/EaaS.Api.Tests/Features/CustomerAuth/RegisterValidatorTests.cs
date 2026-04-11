using EaaS.Api.Features.CustomerAuth;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.CustomerAuth;

public sealed class RegisterValidatorTests
{
    private readonly RegisterValidator _sut = new();

    [Fact]
    public void Should_Pass_WhenValid()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass1", "Acme Corp");

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenNameEmpty()
    {
        var command = new RegisterCommand("", "john@example.com", "SecurePass1", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void Should_Fail_WhenNameTooShort()
    {
        var command = new RegisterCommand("J", "john@example.com", "SecurePass1", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must be at least 2 characters.");
    }

    [Fact]
    public void Should_Fail_WhenEmailInvalid()
    {
        var command = new RegisterCommand("John Doe", "not-an-email", "SecurePass1", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void Should_Fail_WhenEmailEmpty()
    {
        var command = new RegisterCommand("John Doe", "", "SecurePass1", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordTooShort()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "Abc1", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingUppercase()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "securepass1", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingLowercase()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "SECUREPASS1", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingDigit()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass", null);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Should_Fail_WhenCompanyNameTooLong()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass1", new string('A', 201));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CompanyName)
            .WithErrorMessage("Company name must not exceed 200 characters.");
    }

    [Fact]
    public void Should_Pass_WhenCompanyNameNull()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "SecurePass1", null);

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
