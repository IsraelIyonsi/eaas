using EaaS.Shared.Constants;
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
            .Must(x => (x.To?.Count ?? 0) + (x.Cc?.Count ?? 0) + (x.Bcc?.Count ?? 0) <= EmailConstants.MaxRecipientsPerEmail)
            .WithMessage($"Combined To + CC + BCC recipients must not exceed {EmailConstants.MaxRecipientsPerEmail}.")
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
            .Must(tags => tags is null || tags.Count <= EmailConstants.MaxTags)
            .WithMessage($"Maximum {EmailConstants.MaxTags} tags allowed.");

        RuleForEach(x => x.Tags)
            .MaximumLength(EmailConstants.MaxTagLength).WithMessage($"Each tag must not exceed {EmailConstants.MaxTagLength} characters.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(EmailConstants.MaxIdempotencyKeyLength).WithMessage($"Idempotency key must not exceed {EmailConstants.MaxIdempotencyKeyLength} characters.");
    }
}
