using EaaS.Api.Features.CustomerAuth;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.CustomerAuth;

public sealed class RegisterValidatorTests
{
    private const string LegalEntity = "Acme Legal Ltd.";
    private const string PostalAddress = "123 Main St, Lagos, Nigeria";

    private readonly RegisterValidator _sut = new();

    private static RegisterCommand Cmd(
        string name = "John Doe",
        string email = "john@example.com",
        string password = "SecurePass1",
        string? companyName = "Acme Corp",
        string legalEntity = LegalEntity,
        string postalAddress = PostalAddress)
        => new(name, email, password, companyName, legalEntity, postalAddress);

    [Fact]
    public void Should_Pass_WhenValid()
    {
        var result = _sut.TestValidate(Cmd());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenNameEmpty()
    {
        var result = _sut.TestValidate(Cmd(name: ""));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void Should_Fail_WhenNameTooShort()
    {
        var result = _sut.TestValidate(Cmd(name: "J"));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must be at least 2 characters.");
    }

    [Fact]
    public void Should_Fail_WhenEmailInvalid()
    {
        var result = _sut.TestValidate(Cmd(email: "not-an-email"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void Should_Fail_WhenEmailEmpty()
    {
        var result = _sut.TestValidate(Cmd(email: ""));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordTooShort()
    {
        var result = _sut.TestValidate(Cmd(password: "Abc1"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingUppercase()
    {
        var result = _sut.TestValidate(Cmd(password: "securepass1"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingLowercase()
    {
        var result = _sut.TestValidate(Cmd(password: "SECUREPASS1"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordMissingDigit()
    {
        var result = _sut.TestValidate(Cmd(password: "SecurePass"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Should_Fail_WhenCompanyNameTooLong()
    {
        var result = _sut.TestValidate(Cmd(companyName: new string('A', 201)));
        result.ShouldHaveValidationErrorFor(x => x.CompanyName)
            .WithErrorMessage("Company name must not exceed 200 characters.");
    }

    [Fact]
    public void Should_Pass_WhenCompanyNameNull()
    {
        var result = _sut.TestValidate(Cmd(companyName: null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenLegalEntityEmpty()
    {
        var result = _sut.TestValidate(Cmd(legalEntity: ""));
        result.ShouldHaveValidationErrorFor(x => x.LegalEntityName);
    }

    [Fact]
    public void Should_Fail_WhenLegalEntityWhitespace()
    {
        var result = _sut.TestValidate(Cmd(legalEntity: "   "));
        result.ShouldHaveValidationErrorFor(x => x.LegalEntityName);
    }

    [Fact]
    public void Should_Fail_WhenLegalEntityTooLong()
    {
        var result = _sut.TestValidate(Cmd(legalEntity: new string('A', 256)));
        result.ShouldHaveValidationErrorFor(x => x.LegalEntityName)
            .WithErrorMessage("Legal entity name must not exceed 255 characters.");
    }

    [Fact]
    public void Should_Fail_WhenPostalAddressEmpty()
    {
        var result = _sut.TestValidate(Cmd(postalAddress: ""));
        result.ShouldHaveValidationErrorFor(x => x.PostalAddress);
    }

    [Fact]
    public void Should_Fail_WhenPostalAddressWhitespace()
    {
        var result = _sut.TestValidate(Cmd(postalAddress: "   "));
        result.ShouldHaveValidationErrorFor(x => x.PostalAddress);
    }

    [Fact]
    public void Should_Fail_WhenPostalAddressTooLong()
    {
        var result = _sut.TestValidate(Cmd(postalAddress: new string('A', 1001)));
        result.ShouldHaveValidationErrorFor(x => x.PostalAddress)
            .WithErrorMessage("Postal address must not exceed 1000 characters.");
    }
}
