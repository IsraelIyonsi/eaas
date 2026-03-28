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
            .NotNull().WithMessage("At least one recipient is required.")
            .NotEmpty().WithMessage("At least one recipient is required.");

        RuleFor(x => x)
            .Must(x => (x.To?.Count ?? 0) + (x.Cc?.Count ?? 0) + (x.Bcc?.Count ?? 0) <= 50)
            .WithMessage("Combined To + CC + BCC recipients must not exceed 50.")
            .WithName("Recipients");

        RuleForEach(x => x.To)
            .NotEmpty().WithMessage("Recipient email must not be empty.")
            .EmailAddress().WithMessage("Each recipient must be a valid email address.");

        RuleForEach(x => x.Cc)
            .NotEmpty().WithMessage("CC email must not be empty.")
            .EmailAddress().WithMessage("Each CC recipient must be a valid email address.");

        RuleForEach(x => x.Bcc)
            .NotEmpty().WithMessage("BCC email must not be empty.")
            .EmailAddress().WithMessage("Each BCC recipient must be a valid email address.");

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
