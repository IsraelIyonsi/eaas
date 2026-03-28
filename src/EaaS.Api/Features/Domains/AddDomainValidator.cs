using System.Text.RegularExpressions;
using FluentValidation;

namespace EaaS.Api.Features.Domains;

public sealed partial class AddDomainValidator : AbstractValidator<AddDomainCommand>
{
    public AddDomainValidator()
    {
        RuleFor(x => x.DomainName)
            .NotEmpty().WithMessage("Domain name is required.")
            .MaximumLength(253).WithMessage("Domain name must not exceed 253 characters.")
            .Must(BeAValidDomain).WithMessage("Domain name format is invalid.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");
    }

    private static bool BeAValidDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        return DomainRegex().IsMatch(domain);
    }

    [GeneratedRegex(@"^(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.[A-Za-z0-9-]{1,63})*\.[A-Za-z]{2,}$", RegexOptions.Compiled)]
    private static partial Regex DomainRegex();
}
