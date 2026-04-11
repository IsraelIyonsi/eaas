using EaaS.Api.Features.Admin.Auth;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Auth;

public sealed class AdminLoginValidatorTests
{
    private readonly AdminLoginValidator _sut = new();

    [Fact]
    public void Should_Pass_WhenValid()
    {
        var command = TestDataBuilders.AdminLogin().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenEmailEmpty()
    {
        var command = TestDataBuilders.AdminLogin()
            .WithEmail(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Should_Fail_WhenEmailInvalid()
    {
        var command = TestDataBuilders.AdminLogin()
            .WithEmail("not-an-email")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void Should_Fail_WhenPasswordEmpty()
    {
        var command = TestDataBuilders.AdminLogin()
            .WithPassword(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required.");
    }
}
