using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;

namespace EaaS.Infrastructure.Payments;

/// <summary>
/// Resolves the correct IPaymentProvider implementation from DI.
/// All providers are registered as keyed services in DI.
/// </summary>
public sealed class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IReadOnlyDictionary<PaymentProvider, IPaymentProvider> _providers;

    public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderType);
    }

    public IPaymentProvider GetProvider(PaymentProvider provider)
    {
        if (_providers.TryGetValue(provider, out var instance))
            return instance;

        throw new NotSupportedException($"Payment provider '{provider}' is not configured. Available providers: {string.Join(", ", _providers.Keys)}");
    }

    public IEnumerable<PaymentProvider> GetAvailableProviders() => _providers.Keys;
}
