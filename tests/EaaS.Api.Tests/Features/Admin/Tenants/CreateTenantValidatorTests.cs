using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Admin.Tenants;

public sealed class CreateTenantValidatorTests
{
    private readonly CreateTenantValidator _sut = new();

    [Fact]
    public void Should_Pass_When_ValidRequest()
    {
        var command = TestDataBuilders.CreateTenant().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_NameEmpty()
    {
        var command = TestDataBuilders.CreateTenant()
            .WithName(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void Should_Fail_When_NameTooLong()
    {
        var command = TestDataBuilders.CreateTenant()
            .WithName(new string('A', 101))
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not exceed 100 characters.");
    }

    [Fact]
    public void Should_Pass_When_ValidContactEmail()
    {
        var command = TestDataBuilders.CreateTenant()
            .WithContactEmail("contact@example.com")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_InvalidContactEmail()
    {
        var command = TestDataBuilders.CreateTenant()
            .WithContactEmail("not-an-email")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ContactEmail)
            .WithErrorMessage("ContactEmail must be a valid email address.");
    }

    [Fact]
    public void Should_Pass_When_ContactEmailNotProvided()
    {
        var command = TestDataBuilders.CreateTenant()
            .WithContactEmail(null)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.ContactEmail);
    }
}
