using FluentValidation;

namespace EaaS.Api.Features.Suppressions;

public sealed class AddSuppressionValidator : AbstractValidator<AddSuppressionCommand>
{
    public AddSuppressionValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Must be a valid email address.");
    }
}
