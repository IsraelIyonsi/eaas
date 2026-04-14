using System.ComponentModel.DataAnnotations;

namespace EaaS.Infrastructure.EmailProviders.Configuration;

/// <summary>
/// Strongly-typed options for the SES adapter. Bound via
/// <c>AddOptions&lt;SesOptions&gt;().BindConfiguration(EmailProviderConfigKeys.Ses.Section)
///   .ValidateDataAnnotations().ValidateOnStart()</c>.
/// </summary>
public sealed class SesOptions
{
    [Required, MinLength(1)]
    public string AccessKeyId { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string SecretAccessKey { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string Region { get; set; } = "eu-west-1";

    public string? ConfigurationSetName { get; set; }

    /// <summary>
    /// Back-compat: when binding the legacy <c>Ses</c> section we also read <c>MaxSendRate</c>.
    /// Not used by the provider itself — preserved so older appsettings keep loading.
    /// </summary>
    public int MaxSendRate { get; set; } = 14;
}
