using FluentValidation;

namespace EaaS.Api.Features.CustomerAuth;

public sealed class CustomerLoginValidator : AbstractValidator<CustomerLoginCommand>
{
    public CustomerLoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
