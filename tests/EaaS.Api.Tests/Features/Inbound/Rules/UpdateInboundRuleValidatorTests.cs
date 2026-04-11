using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Inbound.Rules;

public sealed class UpdateInboundRuleValidatorTests
{
    private readonly UpdateInboundRuleValidator _sut = new();

    [Fact]
    public void Should_Pass_When_NoChanges()
    {
        var command = TestDataBuilders.UpdateInboundRule().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_When_ValidNameUpdate()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithName("Updated Rule")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_NameEmpty()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithName(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not be empty.");
    }

    [Fact]
    public void Should_Fail_When_NameTooLong()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithName(new string('A', 101))
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not exceed 100 characters.");
    }

    [Fact]
    public void Should_Pass_When_ValidPatternUpdate()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithMatchPattern("support@*")
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_PatternEmpty()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithMatchPattern(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MatchPattern)
            .WithErrorMessage("MatchPattern must not be empty.");
    }

    [Fact]
    public void Should_Fail_When_PatternTooLong()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithMatchPattern(new string('*', 256))
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MatchPattern)
            .WithErrorMessage("MatchPattern must not exceed 255 characters.");
    }

    [Fact]
    public void Should_Fail_When_WebhookActionWithoutUrl()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithAction(InboundRuleAction.Webhook)
            .WithWebhookUrl(null)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.WebhookUrl)
            .WithErrorMessage("WebhookUrl is required when Action is Webhook.");
    }

    [Fact]
    public void Should_Fail_When_ForwardActionWithoutEmail()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithAction(InboundRuleAction.Forward)
            .WithForwardTo(null)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ForwardTo)
            .WithErrorMessage("ForwardTo is required when Action is Forward.");
    }

    [Fact]
    public void Should_Fail_When_InvalidPriority()
    {
        var command = TestDataBuilders.UpdateInboundRule()
            .WithPriority(-1)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Priority.Value")
            .WithErrorMessage("Priority must be greater than or equal to 0.");
    }
}
