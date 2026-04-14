using EaaS.Domain.Enums;
using EaaS.Shared.Utilities;
using FluentValidation;

namespace EaaS.Api.Features.Inbound.Rules;

public sealed class CreateInboundRuleValidator : AbstractValidator<CreateInboundRuleCommand>
{
    public CreateInboundRuleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.DomainId)
            .NotEmpty().WithMessage("DomainId is required.");

        RuleFor(x => x.MatchPattern)
            .NotEmpty().WithMessage("MatchPattern is required.")
            .MaximumLength(255).WithMessage("MatchPattern must not exceed 255 characters.");

        RuleFor(x => x.Action)
            .IsInEnum().WithMessage("Action must be a valid InboundRuleAction value.");

        RuleFor(x => x.WebhookUrl)
            .NotEmpty().WithMessage("WebhookUrl is required when Action is Webhook.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("WebhookUrl must be a valid absolute URL.")
            .Must(url => SsrfGuard.IsSyntacticallySafe(url, out _))
            .WithMessage("WebhookUrl must be HTTPS and must not point to a private, loopback, metadata, or reserved address.")
            .When(x => x.Action == InboundRuleAction.Webhook);

        RuleFor(x => x.ForwardTo)
            .NotEmpty().WithMessage("ForwardTo is required when Action is Forward.")
            .EmailAddress().WithMessage("ForwardTo must be a valid email address.")
            .When(x => x.Action == InboundRuleAction.Forward);

        RuleFor(x => x.Priority)
            .GreaterThanOrEqualTo(0).WithMessage("Priority must be greater than or equal to 0.");
    }
}
