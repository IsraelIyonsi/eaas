namespace EaaS.Infrastructure.EmailProviders.Providers.Mailgun;

/// <summary>
/// Single-source-of-truth provider key for the Mailgun adapter. Mirrors
/// <c>EmailProviderConfigKeys.ProviderKeys.Mailgun</c> so call sites that live
/// inside the <c>Mailgun/</c> folder do not have to reach across to the top-level
/// <c>Configuration</c> namespace.
/// </summary>
public static class MailgunProviderKey
{
    public const string Value = "mailgun";
}
