using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Inbound.Rules;

public sealed class CreateInboundRuleValidatorTests
{
    private readonly CreateInboundRuleValidator _sut = new();

    [Fact]
    public void Should_Pass_WhenValid()
    {
        var command = TestDataBuilders.CreateInboundRule().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenNameEmpty()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithName(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void Should_Fail_WhenMatchPatternEmpty()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithMatchPattern(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MatchPattern)
            .WithErrorMessage("MatchPattern is required.");
    }

    [Fact]
    public void Should_Fail_WhenWebhookAction_WithoutUrl()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithAction(InboundRuleAction.Webhook)
            .WithWebhookUrl(null)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.WebhookUrl)
            .WithErrorMessage("WebhookUrl is required when Action is Webhook.");
    }

    [Fact]
    public void Should_Fail_WhenForwardAction_WithoutEmail()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithAction(InboundRuleAction.Forward)
            .WithForwardTo(null)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ForwardTo)
            .WithErrorMessage("ForwardTo is required when Action is Forward.");
    }

    [Fact]
    public void Should_Fail_WhenPriorityNegative()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithPriority(-1)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority must be greater than or equal to 0.");
    }
}
