using EaaS.Api.Features.Webhooks;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Webhooks;

public sealed class CreateWebhookValidatorTests
{
    private static readonly string[] InvalidEvent = ["invalid_event"];
    private static readonly string[] AllValidEvents = ["sent", "delivered", "bounced", "complained", "opened", "clicked", "failed"];

    private readonly CreateWebhookValidator _sut = new();

    [Fact]
    public void Should_Pass_When_ValidRequest()
    {
        var command = TestDataBuilders.CreateWebhook().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_UrlEmpty()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithUrl(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL is required.");
    }

    [Fact]
    public void Should_Fail_When_UrlNotHttps()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithUrl("http://example.com/webhook")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL must be a valid HTTPS URL.");
    }

    [Fact]
    public void Should_Fail_When_UrlTooLong()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithUrl("https://example.com/" + new string('a', 2030))
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL must not exceed 2048 characters.");
    }

    [Fact]
    public void Should_Fail_When_EventsEmpty()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithEvents(Array.Empty<string>())
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Events)
            .WithErrorMessage("At least one event type is required.");
    }

    [Fact]
    public void Should_Fail_When_InvalidEventType()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithEvents(InvalidEvent)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Events)
            .WithErrorMessage("Events must be one of: sent, delivered, bounced, complained, opened, clicked, failed.");
    }

    [Fact]
    public void Should_Pass_When_AllValidEventTypes()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithEvents(AllValidEvents)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
