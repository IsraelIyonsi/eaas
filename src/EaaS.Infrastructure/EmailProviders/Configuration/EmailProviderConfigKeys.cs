namespace EaaS.Infrastructure.EmailProviders.Configuration;

/// <summary>
/// Single source of truth for every configuration key referenced by the email-provider
/// abstraction. Per the runbook (§6.1) we accept zero magic strings — every appsettings
/// key, env-var dotted path, and provider slug flows through constants defined here.
/// </summary>
public static class EmailProviderConfigKeys
{
    public const string RootSection = "EmailProviders";

    /// <summary>Feature flag: controls whether the abstraction is active. Default <c>true</c>.</summary>
    public const string FeatureFlag = "Features:EmailProviderAbstraction";

    public static class ProviderKeys
    {
        public const string Ses     = "ses";
        public const string Mailgun = "mailgun";
        public const string Smtp    = "smtp";
    }

    public static class Routing
    {
        public const string Section          = "EmailProviders:Routing";
        public const string DefaultProvider  = "EmailProviders:Routing:DefaultProvider";
    }

    public static class Ses
    {
        public const string Section          = "EmailProviders:Ses";
        public const string AccessKeyId      = "EmailProviders:Ses:AccessKeyId";
        public const string SecretAccessKey  = "EmailProviders:Ses:SecretAccessKey";
        public const string Region           = "EmailProviders:Ses:Region";
        public const string ConfigurationSet = "EmailProviders:Ses:ConfigurationSetName";
    }

    public static class Mailgun
    {
        public const string Section           = "EmailProviders:Mailgun";
        public const string ApiKey            = "EmailProviders:Mailgun:ApiKey";
        public const string Region            = "EmailProviders:Mailgun:Region";
        public const string WebhookSigningKey = "EmailProviders:Mailgun:WebhookSigningKey";
        public const string DefaultDomain     = "EmailProviders:Mailgun:DefaultDomain";
    }
}
