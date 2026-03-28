using FluentValidation;

namespace EaaS.Api.Features.Templates;

public sealed class PreviewTemplateValidator : AbstractValidator<PreviewTemplateCommand>
{
    public PreviewTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Template ID is required.");
    }
}
