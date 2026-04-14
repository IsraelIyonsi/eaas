namespace EaaS.Domain.Interfaces;

/// <summary>
/// Result of initiating a payment, including the redirect URL the end-user must visit.
/// </summary>
public sealed record InitiatePaymentResult(
    string ExternalPaymentId,
    string PaymentUrl,
    string Status);
