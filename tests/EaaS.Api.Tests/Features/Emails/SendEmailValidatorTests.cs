using EaaS.Api.Features.Emails;
using EaaS.Api.Tests.Helpers;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Emails;

public sealed class SendEmailValidatorTests
{
    private readonly SendEmailValidator _sut = new();

    [Fact]
    public void Should_Pass_When_ValidRequest()
    {
        var command = TestDataBuilders.SendEmail().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_ToFieldEmpty()
    {
        var command = TestDataBuilders.SendEmail()
            .WithTo(new List<string>())
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Should_Fail_When_FromFieldEmpty()
    {
        var command = TestDataBuilders.SendEmail()
            .WithFrom(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.From);
    }

    [Fact]
    public void Should_Fail_When_NoBodyAndNoTemplate()
    {
        var command = TestDataBuilders.SendEmail()
            .WithHtmlBody(null)
            .WithTextBody(null)
            .WithTemplateId(null)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Body");
    }

    [Fact]
    public void Should_Fail_When_InvalidEmailFormat()
    {
        var command = TestDataBuilders.SendEmail()
            .WithFrom("not-an-email")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.From)
            .WithErrorMessage("From must be a valid email address.");
    }

    [Fact]
    public void Should_Fail_When_TooManyRecipients()
    {
        var recipients = Enumerable.Range(1, 51)
            .Select(i => $"user{i}@example.com")
            .ToList();

        var command = TestDataBuilders.SendEmail()
            .WithTo(recipients)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.To)
            .WithErrorMessage("Maximum 50 recipients allowed.");
    }

    [Fact]
    public void Should_Pass_When_TemplateIdProvidedWithoutBody()
    {
        var command = TestDataBuilders.SendEmail()
            .WithHtmlBody(null)
            .WithTextBody(null)
            .WithSubject(null)
            .WithTemplateId(Guid.NewGuid())
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor("Body");
    }
}
