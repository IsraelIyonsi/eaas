namespace EaaS.Infrastructure.EmailProviders;

/// <summary>
/// Optional hook consumed by <see cref="EmailProviderFactory"/> to look up a
/// tenant's preferred email-provider key (populated by the
/// <c>tenants.preferred_email_provider_key</c> column). Implementations must
/// not throw — lookup errors are logged and swallowed inside the factory, which
/// falls back to the configured default provider.
/// </summary>
public interface ITenantProviderKeyResolver
{
    string? Resolve(Guid tenantId);
}
