namespace EaaS.Domain.Interfaces;

/// <summary>
/// Request payload for initiating a one-off payment with an external payment provider.
/// </summary>
public sealed record InitiatePaymentRequest(
    string ExternalCustomerId,
    decimal AmountInMinorUnits,
    string Currency,
    string Description,
    string CallbackUrl,
    Dictionary<string, string>? Metadata = null);
