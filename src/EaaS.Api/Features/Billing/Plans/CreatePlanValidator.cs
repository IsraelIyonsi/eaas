using FluentValidation;

namespace EaaS.Api.Features.Billing.Plans;

public sealed class CreatePlanValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.MonthlyPriceUsd)
            .GreaterThanOrEqualTo(0).WithMessage("Monthly price must not be negative.");

        RuleFor(x => x.AnnualPriceUsd)
            .GreaterThanOrEqualTo(0).WithMessage("Annual price must not be negative.");

        RuleFor(x => x.DailyEmailLimit)
            .GreaterThan(0).WithMessage("Daily email limit must be greater than zero.");

        RuleFor(x => x.MonthlyEmailLimit)
            .GreaterThan(0).WithMessage("Monthly email limit must be greater than zero.")
            .GreaterThanOrEqualTo(x => x.DailyEmailLimit)
            .WithMessage("Monthly email limit must be greater than or equal to daily email limit.");

        RuleFor(x => x.MaxApiKeys)
            .GreaterThan(0).WithMessage("Max API keys must be greater than zero.");

        RuleFor(x => x.MaxDomains)
            .GreaterThan(0).WithMessage("Max domains must be greater than zero.");

        RuleFor(x => x.MaxTemplates)
            .GreaterThan(0).WithMessage("Max templates must be greater than zero.");

        RuleFor(x => x.MaxWebhooks)
            .GreaterThan(0).WithMessage("Max webhooks must be greater than zero.");
    }
}
