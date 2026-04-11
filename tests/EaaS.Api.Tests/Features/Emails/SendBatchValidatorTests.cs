using EaaS.Api.Features.Emails;
using EaaS.Api.Tests.Helpers;
using EaaS.Shared.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Emails;

public sealed class SendBatchValidatorTests
{
    private readonly SendBatchValidator _sut = new();

    [Fact]
    public void Should_Pass_When_ValidBatch()
    {
        var command = TestDataBuilders.SendBatch().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_EmailsNull()
    {
        var command = TestDataBuilders.SendBatch()
            .WithNullEmails()
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Emails)
            .WithErrorMessage("Emails array is required.");
    }

    [Fact]
    public void Should_Fail_When_EmailsEmpty()
    {
        var command = TestDataBuilders.SendBatch()
            .WithEmails(new List<BatchEmailItem>())
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Emails)
            .WithErrorMessage("At least one email is required.");
    }

    [Fact]
    public void Should_Fail_When_TooManyEmails()
    {
        var emails = Enumerable.Range(1, EmailConstants.MaxBatchSize + 1)
            .Select(i => new BatchEmailItem(
                $"sender{i}@verified.com",
                new List<string> { $"recipient{i}@example.com" },
                null, null, "Subject", "<p>Hello</p>", null,
                null, null, null, null))
            .ToList();

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Emails)
            .WithErrorMessage($"Maximum {EmailConstants.MaxBatchSize} emails per batch.");
    }

    [Fact]
    public void Should_Fail_When_EmailMissingFrom()
    {
        var emails = new List<BatchEmailItem>
        {
            new(string.Empty, new List<string> { "recipient@example.com" },
                null, null, "Subject", "<p>Hello</p>", null,
                null, null, null, null)
        };

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("From address is required.");
    }

    [Fact]
    public void Should_Fail_When_EmailInvalidFrom()
    {
        var emails = new List<BatchEmailItem>
        {
            new("not-an-email", new List<string> { "recipient@example.com" },
                null, null, "Subject", "<p>Hello</p>", null,
                null, null, null, null)
        };

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("From must be a valid email address.");
    }

    [Fact]
    public void Should_Fail_When_EmailMissingTo()
    {
        var emails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string>(),
                null, null, "Subject", "<p>Hello</p>", null,
                null, null, null, null)
        };

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("At least one recipient is required.");
    }

    [Fact]
    public void Should_Fail_When_EmailInvalidTo()
    {
        var emails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "not-an-email" },
                null, null, "Subject", "<p>Hello</p>", null,
                null, null, null, null)
        };

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("Each recipient must be a valid email address.");
    }

    [Fact]
    public void Should_Fail_When_MissingSubjectWithoutTemplate()
    {
        var emails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "recipient@example.com" },
                null, null, string.Empty, "<p>Hello</p>", null,
                null, null, null, null)
        };

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("Subject is required when no template is used.");
    }

    [Fact]
    public void Should_Pass_When_SubjectEmptyWithTemplate()
    {
        var emails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "recipient@example.com" },
                null, null, null, null, null,
                Guid.NewGuid(), null, null, null)
        };

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_MissingBodyWithoutTemplate()
    {
        var emails = new List<BatchEmailItem>
        {
            new("sender@verified.com", new List<string> { "recipient@example.com" },
                null, null, "Subject", null, null,
                null, null, null, null)
        };

        var command = TestDataBuilders.SendBatch()
            .WithEmails(emails)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("Either htmlBody, textBody, or templateId is required.");
    }
}
