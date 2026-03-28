using FluentValidation;

namespace EaaS.Api.Features.ApiKeys;

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");
    }
}
