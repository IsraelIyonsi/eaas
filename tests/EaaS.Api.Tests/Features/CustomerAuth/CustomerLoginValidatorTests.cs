using EaaS.Api.Features.CustomerAuth;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.CustomerAuth;

public sealed class CustomerLoginValidatorTests
{
    private readonly CustomerLoginValidator _sut = new();

    [Fact]
    public void Should_Pass_WithValidInput()
    {
        var command = new CustomerLoginCommand("john@example.com", "SecurePass1");

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenEmailEmpty()
    {
        var command = new CustomerLoginCommand("", "SecurePass1");

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Fail_WhenPasswordEmpty()
    {
        var command = new CustomerLoginCommand("john@example.com", "");

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_Fail_WhenEmailInvalidFormat()
    {
        var command = new CustomerLoginCommand("not-an-email", "SecurePass1");

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
