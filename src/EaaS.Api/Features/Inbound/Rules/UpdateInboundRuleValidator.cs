using EaaS.Domain.Enums;
using EaaS.Shared.Utilities;
using FluentValidation;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed class UpdateInboundRuleValidator : AbstractValidator<UpdateInboundRuleCommand>
{
    public UpdateInboundRuleValidator()
    {
        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!)
                .NotEmpty().WithMessage("Name must not be empty.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
        });

        When(x => x.MatchPattern is not null, () =>
        {
            RuleFor(x => x.MatchPattern!)
                .NotEmpty().WithMessage("MatchPattern must not be empty.")
                .MaximumLength(255).WithMessage("MatchPattern must not exceed 255 characters.");
        });

        When(x => x.Action.HasValue, () =>
        {
            RuleFor(x => x.Action!.Value)
                .IsInEnum().WithMessage("Action must be a valid InboundRuleAction value.");
        });

        When(x => x.Action == InboundRuleAction.Webhook, () =>
        {
            RuleFor(x => x.WebhookUrl)
                .NotEmpty().WithMessage("WebhookUrl is required when Action is Webhook.")
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("WebhookUrl must be a valid absolute URL.")
                .Must(url => url is null || SsrfGuard.IsSyntacticallySafe(url, out _))
                .WithMessage("WebhookUrl must be HTTPS and must not point to a private, loopback, metadata, or reserved address.");
        });

        When(x => x.Action == InboundRuleAction.Forward, () =>
        {
            RuleFor(x => x.ForwardTo)
                .NotEmpty().WithMessage("ForwardTo is required when Action is Forward.")
                .EmailAddress().WithMessage("ForwardTo must be a valid email address.");
        });

        When(x => x.Priority.HasValue, () =>
        {
            RuleFor(x => x.Priority!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("Priority must be greater than or equal to 0.");
        });
    }
}
