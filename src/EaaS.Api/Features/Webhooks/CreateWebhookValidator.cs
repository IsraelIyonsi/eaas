using EaaS.Shared.Utilities;
using FluentValidation;

namespace EaaS.Api.Features.Webhooks;

public sealed class CreateWebhookValidator : AbstractValidator<CreateWebhookCommand>
{
    private static readonly string[] ValidEvents =
        { "queued", "sent", "delivered", "bounced", "complained", "opened", "clicked", "failed" };

    public CreateWebhookValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.")
            .MaximumLength(2048).WithMessage("URL must not exceed 2048 characters.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            .WithMessage("URL must be a valid HTTPS URL.");

        // BUG-M4: surface the SsrfGuard's structured rejection reason instead of a canned
        // message. `Custom` lets us write the actual class-of-rejection (e.g. "URL must
        // use HTTPS", "URL hostname is not permitted") while the guard itself still redacts
        // specific IPs — no raw addresses end up in the 400 body.
        RuleFor(x => x.Url)
            .Custom((url, ctx) =>
            {
                if (string.IsNullOrWhiteSpace(url)) return; // NotEmpty already caught it.
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                    return; // HTTPS check already caught it.

                if (!SsrfGuard.IsSyntacticallySafe(url, out var reason))
                {
                    ctx.AddFailure(nameof(CreateWebhookCommand.Url),
                        reason ?? "URL must not point to a private, loopback, metadata, or reserved address.");
                }
            });

        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("At least one event type is required.")
            .Must(events => events.All(e => ValidEvents.Contains(e.ToLowerInvariant())))
            .WithMessage($"Events must be one of: {string.Join(", ", ValidEvents)}.");
    }
}
