using EaaS.Api.Features.Webhooks;
using EaaS.Api.Tests.Helpers;
using FluentAssertions;
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
            .WithErrorMessage("Events must be one of: queued, sent, delivered, bounced, complained, opened, clicked, failed.");
    }

    [Theory]
    [InlineData("https://169.254.169.254/latest/meta-data/")] // AWS IMDS
    [InlineData("https://10.0.0.1/hook")]                     // RFC1918
    [InlineData("https://192.168.1.1/hook")]                  // RFC1918
    [InlineData("https://127.0.0.1/hook")]                    // loopback
    [InlineData("https://localhost/hook")]                    // blocked hostname
    [InlineData("https://foo.internal/hook")]                 // blocked suffix
    [InlineData("https://[fd00:ec2::254]/meta")]              // AWS IMDS v6 / ULA
    public void Should_Fail_When_UrlTargetsSsrfRange(string url)
    {
        var command = TestDataBuilders.CreateWebhook().WithUrl(url).Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    // BUG-M4: SSRF rejection reason must be passed through from SsrfGuard — the
    // caller should see a specific class-of-rejection message, not the old canned
    // "private, loopback, metadata, or reserved" line for every case.
    [Fact]
    public void BugM4_Should_SurfaceSpecificReason_When_HostnameBlocked()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithUrl("https://localhost/hook")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url);
        // SsrfGuard's host-based rejection reason should come through verbatim.
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebhookCommand.Url)
            && e.ErrorMessage.Contains("hostname", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BugM4_Should_SurfaceSpecificReason_When_PrivateIp()
    {
        var command = TestDataBuilders.CreateWebhook()
            .WithUrl("https://10.0.0.1/hook")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Url);
        // SsrfGuard's IP-class reason should come through — mentions the class, not the raw IP.
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebhookCommand.Url)
            && (e.ErrorMessage.Contains("private", StringComparison.OrdinalIgnoreCase)
                || e.ErrorMessage.Contains("reserved", StringComparison.OrdinalIgnoreCase)));
        // Must not echo the raw IP literal back at the customer.
        result.Errors.Where(e => e.PropertyName == nameof(CreateWebhookCommand.Url))
            .Should().NotContain(e => e.ErrorMessage.Contains("10.0.0.1"));
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
