using FluentValidation;

namespace EaaS.Api.Features.Admin.Tenants;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ContactEmail)
            .EmailAddress().WithMessage("ContactEmail must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

        RuleFor(x => x.LegalEntityName)
            .NotEmpty().WithMessage("Legal entity name is required (CAN-SPAM §7704(a)(5)).")
            .MaximumLength(255);

        RuleFor(x => x.PostalAddress)
            .NotEmpty().WithMessage("Postal address is required (CAN-SPAM §7704(a)(5)).")
            .MaximumLength(1000);
    }
}
