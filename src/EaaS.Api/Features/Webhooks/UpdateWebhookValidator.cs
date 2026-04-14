using EaaS.Shared.Utilities;
using FluentValidation;

namespace EaaS.Api.Features.Webhooks;

public sealed class UpdateWebhookValidator : AbstractValidator<UpdateWebhookCommand>
{
    private static readonly string[] ValidEvents =
        { "queued", "sent", "delivered", "bounced", "complained", "opened", "clicked", "failed" };

    public UpdateWebhookValidator()
    {
        When(x => x.Url is not null, () =>
        {
            RuleFor(x => x.Url!)
                .MaximumLength(2048).WithMessage("URL must not exceed 2048 characters.")
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                .WithMessage("URL must be a valid HTTPS URL.");

            // BUG-M4: surface the SsrfGuard's structured rejection reason.
            RuleFor(x => x.Url!)
                .Custom((url, ctx) =>
                {
                    if (string.IsNullOrWhiteSpace(url)) return;
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                        return;

                    if (!SsrfGuard.IsSyntacticallySafe(url, out var reason))
                    {
                        ctx.AddFailure(nameof(UpdateWebhookCommand.Url),
                            reason ?? "URL must not point to a private, loopback, metadata, or reserved address.");
                    }
                });
        });

        When(x => x.Events is not null, () =>
        {
            RuleFor(x => x.Events!)
                .NotEmpty().WithMessage("At least one event type is required.")
                .Must(events => events.All(e => ValidEvents.Contains(e.ToLowerInvariant())))
                .WithMessage($"Events must be one of: {string.Join(", ", ValidEvents)}.");
        });
    }
}
