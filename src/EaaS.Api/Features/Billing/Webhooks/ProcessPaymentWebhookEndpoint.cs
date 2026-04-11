using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Webhooks;

public static class ProcessPaymentWebhookEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{provider}", async (
            string provider,
            HttpContext context,
            IMediator mediator) =>
        {
            var payload = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var signature = context.Request.Headers["X-Paystack-Signature"].FirstOrDefault()
                ?? context.Request.Headers["Stripe-Signature"].FirstOrDefault()
                ?? string.Empty;

            await mediator.Send(new ProcessPaymentWebhookCommand(provider, payload, signature));
            return Results.Ok(ApiResponse.Ok<object?>(null));
        })
        .WithName("ProcessPaymentWebhook")
        .AllowAnonymous();
    }
}
