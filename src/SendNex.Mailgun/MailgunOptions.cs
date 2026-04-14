using System.ComponentModel.DataAnnotations;

namespace SendNex.Mailgun;

/// <summary>
/// Strongly-typed options for the Mailgun typed <see cref="System.Net.Http.HttpClient"/>.
/// Binds to the <c>EmailProviders:Mailgun</c> configuration section; every field
/// validated on start so a misconfigured production deploy fails immediately.
/// </summary>
public sealed class MailgunOptions
{
    /// <summary>Master API key (Flex launch tier: single shared key, stored in secret store).</summary>
    [Required, MinLength(1)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Mailgun REST base URL. Defaults to the US region (<see cref="MailgunConstants.DefaultApiBaseUrl"/>).</summary>
    [Required, MinLength(1)]
    public string ApiBaseUrl { get; set; } = MailgunConstants.DefaultApiBaseUrl;

    /// <summary>HMAC signing key used to verify inbound webhooks (distinct from <see cref="ApiKey"/>).</summary>
    [Required, MinLength(1)]
    public string WebhookSigningKey { get; set; } = string.Empty;

    /// <summary>Region key — <c>US</c> or <c>EU</c>. Purely informational at Flex tier; base URL is authoritative.</summary>
    [RegularExpression("^(US|EU)$", ErrorMessage = "DefaultRegion must be either 'US' or 'EU'.")]
    public string DefaultRegion { get; set; } = MailgunConstants.Regions.Us;

    /// <summary>Fallback sending domain (per-tenant domain overrides take precedence in the adapter).</summary>
    public string? DefaultSendingDomain { get; set; }

    /// <summary>Master feature gate — when <c>false</c> the adapter is skipped at DI time.</summary>
    public bool Enabled { get; set; }

    /// <summary>Request timeout in seconds for Mailgun REST calls. Defaults to 30s.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 30;
}
