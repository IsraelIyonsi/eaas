using EaaS.Domain.Enums;

namespace EaaS.Domain.Interfaces;

/// <summary>
/// Abstraction for payment provider operations.
/// Each provider (Stripe, PayStack, Flutterwave, PayPal) implements this interface.
/// </summary>
public interface IPaymentProvider
{
    PaymentProvider ProviderType { get; }

    // Customer management
    Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<bool> DeleteCustomerAsync(string externalCustomerId, CancellationToken ct = default);

    // Subscription management
    Task<CreateSubscriptionResult> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default);
    Task<bool> CancelSubscriptionAsync(string externalSubscriptionId, bool immediate, CancellationToken ct = default);
    Task<SubscriptionInfo> GetSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default);

    // Payment
    Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken ct = default);
    Task<bool> VerifyPaymentAsync(string externalPaymentId, CancellationToken ct = default);

    // Webhook verification
    Task<WebhookEvent?> ParseWebhookAsync(string payload, string signature, CancellationToken ct = default);
}
