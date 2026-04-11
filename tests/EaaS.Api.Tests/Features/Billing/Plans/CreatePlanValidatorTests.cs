using EaaS.Api.Features.Billing.Plans;
using EaaS.Api.Tests.Helpers;
using EaaS.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Plans;

public sealed class CreatePlanValidatorTests
{
    private readonly CreatePlanValidator _sut = new();

    [Fact]
    public void Should_Pass_WithValidInput()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithName("Pro Plan")
            .WithTier(PlanTier.Pro)
            .WithMonthlyPriceUsd(29.99m)
            .WithDailyEmailLimit(10000)
            .WithMonthlyEmailLimit(300000)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenNameEmpty()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithName(string.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Fail_WhenNameTooLong()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithName(new string('A', 101))
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Fail_WhenMonthlyPriceNegative()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithMonthlyPriceUsd(-1m)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MonthlyPriceUsd);
    }

    [Fact]
    public void Should_Fail_WhenDailyEmailLimitZero()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithDailyEmailLimit(0)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DailyEmailLimit);
    }

    [Fact]
    public void Should_Fail_WhenMonthlyEmailLimitLessThanDailyLimit()
    {
        var command = TestDataBuilders.CreatePlan()
            .WithDailyEmailLimit(1000)
            .WithMonthlyEmailLimit(500)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MonthlyEmailLimit);
    }
}
