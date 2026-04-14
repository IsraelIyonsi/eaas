namespace EaaS.Domain.Providers;

/// <summary>
/// Resolves the outbound <see cref="IEmailProvider"/> for a given tenant. Phase 0
/// returns the shared default (SES) for every tenant; per-tenant preference
/// (<c>tenants.preferred_email_provider_key</c>) and Mailgun routing land in Phase 1.
/// </summary>
public interface IEmailProviderFactory
{
    IEmailProvider GetForTenant(Guid tenantId);
}
