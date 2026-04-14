using FluentValidation;

namespace EaaS.Api.Features.CustomerAuth;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.LegalEntityName)
            .NotEmpty().WithMessage("Legal entity name is required (CAN-SPAM §7704(a)(5)).")
            .MaximumLength(255).WithMessage("Legal entity name must not exceed 255 characters.");

        RuleFor(x => x.PostalAddress)
            .NotEmpty().WithMessage("Postal address is required (CAN-SPAM §7704(a)(5)).")
            .MaximumLength(1000).WithMessage("Postal address must not exceed 1000 characters.");
    }
}
