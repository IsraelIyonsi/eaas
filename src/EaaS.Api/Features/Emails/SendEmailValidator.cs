using FluentValidation;

namespace EaaS.Api.Features.Emails;

public sealed class SendEmailValidator : AbstractValidator<SendEmailCommand>
{
    public SendEmailValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From address is required.")
            .EmailAddress().WithMessage("From must be a valid email address.");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("At least one recipient is required.")
            .Must(to => to.Count <= 50).WithMessage("Maximum 50 recipients allowed.");

        RuleForEach(x => x.To)
            .NotEmpty().WithMessage("Recipient email must not be empty.")
            .EmailAddress().WithMessage("Each recipient must be a valid email address.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required when no template is used.")
            .When(x => x.TemplateId is null);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.HtmlBody)
                        || !string.IsNullOrWhiteSpace(x.TextBody)
                        || x.TemplateId is not null)
            .WithMessage("Either htmlBody, textBody, or templateId is required.")
            .WithName("Body");

        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 10)
            .WithMessage("Maximum 10 tags allowed.");

        RuleForEach(x => x.Tags)
            .MaximumLength(50).WithMessage("Each tag must not exceed 50 characters.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(255).WithMessage("Idempotency key must not exceed 255 characters.");
    }
}
