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
            .WithPassword("short")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
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
