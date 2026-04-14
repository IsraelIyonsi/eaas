using EaaS.Shared.Constants;
using FluentValidation;

namespace EaaS.Api.Features.Templates;

public sealed class UpdateTemplateValidator : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Template ID is required.");

        RuleFor(x => x.Name)
            .MaximumLength(EmailConstants.MaxTemplateNameLength).WithMessage($"Template name must not exceed {EmailConstants.MaxTemplateNameLength} characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.HtmlTemplate)
            .Must(h => h is null || h.Length <= 524288)
            .WithMessage("HTML template must not exceed 512KB.");
    }
}
