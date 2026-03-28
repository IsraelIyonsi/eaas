using FluentValidation;

namespace EaaS.Api.Features.Templates;

public sealed class UpdateTemplateValidator : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Template ID is required.");

        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("Template name must not exceed 100 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.HtmlBody)
            .Must(h => h is null || h.Length <= 524288)
            .WithMessage("HTML body must not exceed 512KB.");
    }
}
