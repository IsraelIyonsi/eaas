using EaaS.Api.Features.Billing.Subscriptions;
using EaaS.Api.Tests.Helpers;
using FluentValidation.TestHelper;
using Xunit;

namespace EaaS.Api.Tests.Features.Billing.Subscriptions;

public sealed class CreateSubscriptionValidatorTests
{
    private readonly CreateSubscriptionValidator _sut = new();

    [Fact]
    public void Should_Pass_When_ValidRequest()
    {
        var command = TestDataBuilders.CreateSubscription().Build();

        var result = _sut.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_TenantIdEmpty()
    {
        var command = TestDataBuilders.CreateSubscription()
            .WithTenantId(Guid.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantId)
            .WithErrorMessage("TenantId is required.");
    }

    [Fact]
    public void Should_Fail_When_PlanIdEmpty()
    {
        var command = TestDataBuilders.CreateSubscription()
            .WithPlanId(Guid.Empty)
            .Build();

        var result = _sut.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PlanId)
            .WithErrorMessage("PlanId is required.");
    }
}
