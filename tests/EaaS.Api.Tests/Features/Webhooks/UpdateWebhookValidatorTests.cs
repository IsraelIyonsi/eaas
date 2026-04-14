using EaaS.Api.Features.Webhooks;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Webhooks;

public sealed class UpdateWebhookValidatorTests
{
    private static readonly string[] ValidEvents = ["sent", "delivered"];
    private static readonly string[] InvalidEvent = ["invalid_event"];

    private readonly UpdateWebhookValidator _sut = new();

    [Fact]
    public void Should_Pass_When_NoChanges()
    {
        var command = TestDataBuilders.UpdateWebhook().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_When_ValidUrlUpdate()
    {
        var command = TestDataBuilders.UpdateWebhook()
            .WithUrl("https://example.com/new-webhook")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_UrlNotHttps()
    {
        var command = TestDataBuilders.UpdateWebhook()
            .WithUrl("http://example.com/webhook")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL must be a valid HTTPS URL.");
    }

    [Fact]
    public void Should_Fail_When_UrlTooLong()
    {
        var command = TestDataBuilders.UpdateWebhook()
            .WithUrl("https://example.com/" + new string('a', 2030))
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL must not exceed 2048 characters.");
    }

    [Fact]
    public void Should_Pass_When_ValidEventsUpdate()
    {
        var command = TestDataBuilders.UpdateWebhook()
            .WithEvents(ValidEvents)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_EventsListEmpty()
    {
        var command = TestDataBuilders.UpdateWebhook()
            .WithEvents(Array.Empty<string>())
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Events)
            .WithErrorMessage("At least one event type is required.");
    }

    [Fact]
    public void Should_Fail_When_InvalidEvent()
    {
        var command = TestDataBuilders.UpdateWebhook()
            .WithEvents(InvalidEvent)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Events)
            .WithErrorMessage("Events must be one of: queued, sent, delivered, bounced, complained, opened, clicked, failed.");
    }
}
