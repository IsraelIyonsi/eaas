using EaaS.Api.Features.Emails;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Emails;

public sealed class ScheduleEmailValidatorTests
{
    private readonly ScheduleEmailValidator _sut = new();

    [Fact]
    public void Should_Pass_WithValidScheduledEmail()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenFromEmpty()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: string.Empty,
            To: "recipient@example.com",
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.From)
            .WithErrorMessage("Sender email is required.");
    }

    [Fact]
    public void Should_Fail_WhenFromInvalidEmail()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "not-an-email",
            To: "recipient@example.com",
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.From)
            .WithErrorMessage("Sender must be a valid email address.");
    }

    [Fact]
    public void Should_Fail_WhenSubjectTooLong()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: new string('A', 999),
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Subject)
            .WithErrorMessage("Subject must not exceed 998 characters.");
    }

    [Fact]
    public void Should_Fail_WhenToEmpty()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: string.Empty,
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Should_Fail_WhenSubjectEmpty()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: string.Empty,
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Should_Fail_WhenScheduledAtEmpty()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: default);

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ScheduledAt);
    }

    [Fact]
    public void Should_Fail_WhenScheduledAtInPast()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddMinutes(-5));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ScheduledAt);
    }

    [Fact]
    public void Should_Fail_WhenScheduledAtMoreThan30DaysOut()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddDays(31));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ScheduledAt);
    }

    [Fact]
    public void Should_Fail_WhenNoBodyAndNoTemplate()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "recipient@example.com",
            Subject: "Test Subject",
            HtmlBody: null,
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Body");
    }

    [Fact]
    public void Should_Fail_WhenToIsInvalidEmail()
    {
        var command = new ScheduleEmailCommand(
            TenantId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            From: "sender@example.com",
            To: "not-an-email",
            Subject: "Test Subject",
            HtmlBody: "<p>Hello</p>",
            TextBody: null,
            TemplateId: null,
            Variables: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1));

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.To);
    }
}
