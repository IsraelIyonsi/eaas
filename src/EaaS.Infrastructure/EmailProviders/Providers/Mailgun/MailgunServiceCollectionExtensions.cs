using EaaS.Domain.Providers;
using EaaS.Infrastructure.EmailProviders.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SendNex.Mailgun;

namespace EaaS.Infrastructure.EmailProviders.Providers.Mailgun;

/// <summary>
/// Phase 1 wiring for the Mailgun adapter. Registered via the explicit
/// <c>AddMailgunEmailProvider</c> call site (not <c>AddEmailProviders</c>) so the
/// default SES/SMTP path stays the strangler-fig baseline until tenants are
/// actively cut over. When <c>EmailProviders:Mailgun:Enabled</c> is <c>false</c>
/// (or the section is absent) this registration is a no-op.
/// </summary>
public static class MailgunServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MailgunEmailProvider"/>, <see cref="MailgunWebhookSignatureVerifier"/>,
    /// and <see cref="MailgunEmailEventNormalizer"/> behind the standard Domain
    /// interfaces, plus the typed <see cref="IMailgunClient"/> from <see cref="SendNex.Mailgun"/>.
    /// </summary>
    public static IServiceCollection AddMailgunEmailProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(EmailProviderConfigKeys.Mailgun.Section);
        if (!section.Exists()) return services;

        var enabled = section.GetValue<bool?>("Enabled") ?? false;
        if (!enabled) return services;

        services.AddMailgunHttpClient(EmailProviderConfigKeys.Mailgun.Section);

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<MailgunEmailProvider>();
        services.AddSingleton<IEmailProvider>(sp => sp.GetRequiredService<MailgunEmailProvider>());

        services.AddSingleton<MailgunWebhookSignatureVerifier>();
        services.AddSingleton<IWebhookSignatureVerifier>(sp =>
            sp.GetRequiredService<MailgunWebhookSignatureVerifier>());

        services.AddSingleton<MailgunEmailEventNormalizer>();
        services.AddSingleton<IEmailEventNormalizer>(sp =>
            sp.GetRequiredService<MailgunEmailEventNormalizer>());

        return services;
    }
}
