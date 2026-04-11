using FluentValidation;

namespace EaaS.Api.Features.Emails;

public sealed class ScheduleEmailValidator : AbstractValidator<ScheduleEmailCommand>
{
    public ScheduleEmailValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("Sender email is required.")
            .EmailAddress().WithMessage("Sender must be a valid email address.");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Recipient email is required.")
            .EmailAddress().WithMessage("Recipient must be a valid email address.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(998).WithMessage("Subject must not exceed 998 characters.");

        RuleFor(x => x.ScheduledAt)
            .NotEmpty().WithMessage("Scheduled time is required.")
            .GreaterThan(DateTime.UtcNow).WithMessage("Scheduled time must be in the future.")
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(30)).WithMessage("Scheduled time must be within 30 days.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.HtmlBody)
                        || !string.IsNullOrWhiteSpace(x.TextBody)
                        || x.TemplateId is not null)
            .WithMessage("Either htmlBody, textBody, or templateId is required.")
            .WithName("Body");
    }
}
