using FluentValidation;

namespace EaaS.Api.Features.Templates;

public sealed class RollbackTemplateValidator : AbstractValidator<RollbackTemplateCommand>
{
    public RollbackTemplateValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.TargetVersion).GreaterThan(0).WithMessage("Version must be a positive integer.");
    }
}
