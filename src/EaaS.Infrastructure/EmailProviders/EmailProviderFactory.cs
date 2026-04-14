using EaaS.Domain.Providers;
using EaaS.Infrastructure.EmailProviders.Configuration;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.EmailProviders;

/// <summary>
/// Phase 1 <see cref="IEmailProviderFactory"/>. Resolution order:
/// <list type="number">
///   <item>Per-tenant preference via <see cref="ITenantProviderKeyResolver"/>, when
///         the resolver is registered and returns a key matching a registered
///         adapter (e.g. <c>"mailgun"</c>).</item>
///   <item>The configured <c>EmailProviders:Routing:DefaultProvider</c> adapter.</item>
/// </list>
/// Per-tenant lookup failures (resolver exception, unknown key, disabled adapter)
/// fall through to the default — never blocks a send.
/// </summary>
internal sealed partial class EmailProviderFactory : IEmailProviderFactory
{
    private readonly IReadOnlyDictionary<string, IEmailProvider> _providers;
    private readonly string _defaultKey;
    private readonly ITenantProviderKeyResolver? _tenantResolver;
    private readonly ILogger<EmailProviderFactory> _logger;

    public EmailProviderFactory(
        IEnumerable<IEmailProvider> providers,
        string defaultKey,
        ILogger<EmailProviderFactory> logger,
        ITenantProviderKeyResolver? tenantResolver = null)
    {
        _providers = providers.ToDictionary(p => p.ProviderKey, StringComparer.OrdinalIgnoreCase);
        _defaultKey = defaultKey;
        _logger = logger;
        _tenantResolver = tenantResolver;

        if (_providers.Count == 0)
            throw new InvalidOperationException("No IEmailProvider implementations registered.");

        if (!_providers.ContainsKey(_defaultKey))
            throw new InvalidOperationException(
                $"Default email provider '{_defaultKey}' is not registered. " +
                $"Registered: {string.Join(", ", _providers.Keys)}.");
    }

    public IEmailProvider GetForTenant(Guid tenantId)
    {
        if (_tenantResolver is not null)
        {
            string? preferred = null;
            try
            {
                preferred = _tenantResolver.Resolve(tenantId);
            }
            catch (Exception ex)
            {
                LogTenantResolveFailed(_logger, ex, tenantId);
            }

            if (!string.IsNullOrWhiteSpace(preferred) &&
                _providers.TryGetValue(preferred, out var match))
            {
                LogProviderResolved(_logger, tenantId, match.ProviderKey);
                return match;
            }
        }

        var fallback = _providers[_defaultKey];
        LogProviderResolved(_logger, tenantId, fallback.ProviderKey);
        return fallback;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved email provider '{ProviderKey}' for tenant {TenantId}")]
    private static partial void LogProviderResolved(ILogger logger, Guid tenantId, string providerKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tenant provider preference lookup failed for {TenantId}; falling back to default.")]
    private static partial void LogTenantResolveFailed(ILogger logger, Exception ex, Guid tenantId);
}
