using FluentValidation;

namespace EaaS.Api.Features.Templates;

public sealed class CreateTemplateValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(100).WithMessage("Template name must not exceed 100 characters.");

        RuleFor(x => x.SubjectTemplate)
            .NotEmpty().WithMessage("Subject template is required.");

        RuleFor(x => x.HtmlBody)
            .NotEmpty().WithMessage("HTML body is required.")
            .Must(h => h is null || h.Length <= 524288)
            .WithMessage("HTML body must not exceed 512KB.");
    }
}
