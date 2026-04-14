namespace EaaS.Domain.Interfaces;

/// <summary>
/// Result of creating a customer with an external payment provider.
/// </summary>
public sealed record CreateCustomerResult(
    string ExternalCustomerId,
    string Email);
