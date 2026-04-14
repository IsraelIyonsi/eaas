using EaaS.Domain.Providers;
using EaaS.Infrastructure.EmailProviders.Configuration;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.EmailProviders;

/// <summary>
/// Phase 0 <see cref="IEmailProviderFactory"/>. Selects an adapter by the registered
/// default provider key (<c>EmailProviders:Routing:DefaultProvider</c>). Per-tenant
/// preference (<c>tenants.preferred_email_provider_key</c>) is wired in Phase 1
/// alongside the Mailgun adapter. Today every tenant resolves to the same adapter.
/// </summary>
internal sealed partial class EmailProviderFactory : IEmailProviderFactory
{
    private readonly IReadOnlyDictionary<string, IEmailProvider> _providers;
    private readonly string _defaultKey;
    private readonly ILogger<EmailProviderFactory> _logger;

    public EmailProviderFactory(
        IEnumerable<IEmailProvider> providers,
        string defaultKey,
        ILogger<EmailProviderFactory> logger)
    {
        _providers = providers.ToDictionary(p => p.ProviderKey, StringComparer.OrdinalIgnoreCase);
        _defaultKey = defaultKey;
        _logger = logger;

        if (_providers.Count == 0)
            throw new InvalidOperationException("No IEmailProvider implementations registered.");

        if (!_providers.ContainsKey(_defaultKey))
            throw new InvalidOperationException(
                $"Default email provider '{_defaultKey}' is not registered. " +
                $"Registered: {string.Join(", ", _providers.Keys)}.");
    }

    public IEmailProvider GetForTenant(Guid tenantId)
    {
        // Phase 0 — every tenant uses the default adapter. Per-tenant routing lands in Phase 1.
        LogProviderResolved(_logger, tenantId, _defaultKey);
        return _providers[_defaultKey];
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved email provider '{ProviderKey}' for tenant {TenantId}")]
    private static partial void LogProviderResolved(ILogger logger, Guid tenantId, string providerKey);
}
