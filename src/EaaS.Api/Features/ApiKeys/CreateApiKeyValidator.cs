using EaaS.Shared.Constants;
using FluentValidation;

namespace EaaS.Api.Features.ApiKeys;

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(ApiKeyConstants.MaxNameLength).WithMessage($"Name must not exceed {ApiKeyConstants.MaxNameLength} characters.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");
    }
}
