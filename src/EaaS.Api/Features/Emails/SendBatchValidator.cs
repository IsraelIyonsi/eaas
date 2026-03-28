using FluentValidation;

namespace EaaS.Api.Features.Emails;

public sealed class SendBatchValidator : AbstractValidator<SendBatchCommand>
{
    public SendBatchValidator()
    {
        RuleFor(x => x.Emails)
            .NotNull().WithMessage("Emails array is required.")
            .NotEmpty().WithMessage("At least one email is required.")
            .Must(e => e is null || e.Count <= 100).WithMessage("Maximum 100 emails per batch.");

        RuleForEach(x => x.Emails).ChildRules(email =>
        {
            email.RuleFor(e => e.From)
                .NotEmpty().WithMessage("From address is required.")
                .EmailAddress().WithMessage("From must be a valid email address.");

            email.RuleFor(e => e.To)
                .NotNull().WithMessage("At least one recipient is required.")
                .NotEmpty().WithMessage("At least one recipient is required.");

            email.RuleForEach(e => e.To)
                .NotEmpty().WithMessage("Recipient email must not be empty.")
                .EmailAddress().WithMessage("Each recipient must be a valid email address.");

            email.RuleFor(e => e.Subject)
                .NotEmpty().WithMessage("Subject is required when no template is used.")
                .When(e => e.TemplateId is null);

            email.RuleFor(e => e)
                .Must(e => !string.IsNullOrWhiteSpace(e.HtmlBody)
                            || !string.IsNullOrWhiteSpace(e.TextBody)
                            || e.TemplateId is not null)
                .WithMessage("Either htmlBody, textBody, or templateId is required.")
                .WithName("Body");
        });
    }
}
