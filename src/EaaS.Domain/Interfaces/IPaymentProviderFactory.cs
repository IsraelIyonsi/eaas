using EaaS.Domain.Enums;

namespace EaaS.Domain.Interfaces;

/// <summary>
/// Factory for resolving the correct payment provider implementation.
/// Usage: var provider = factory.GetProvider(PaymentProvider.Stripe);
/// </summary>
public interface IPaymentProviderFactory
{
    IPaymentProvider GetProvider(PaymentProvider provider);
    IEnumerable<PaymentProvider> GetAvailableProviders();
}
