namespace EaaS.Domain.Interfaces;

/// <summary>
/// Request payload for creating a customer with an external payment provider.
/// </summary>
public sealed record CreateCustomerRequest(
    string Email,
    string Name,
    string? CompanyName,
    Dictionary<string, string>? Metadata = null);
