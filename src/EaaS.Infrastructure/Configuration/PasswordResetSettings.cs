namespace EaaS.Infrastructure.Configuration;

/// <summary>
/// Configuration for the self-serve password reset flow.
/// </summary>
public sealed class PasswordResetSettings
{
    public const string SectionName = "PasswordReset";

    /// <summary>HMAC secret used to derive reset tokens.</summary>
    public string HmacSecret { get; set; } = "";

    /// <summary>Base URL of the dashboard used to build reset links, e.g. https://app.sendnex.xyz.</summary>
    public string DashboardBaseUrl { get; set; } = "https://app.sendnex.xyz";

    /// <summary>Sender address for password reset emails.</summary>
    public string SystemSender { get; set; } = "sendnex-ops@sendnex.xyz";

    /// <summary>Display name for the sender.</summary>
    public string SystemSenderName { get; set; } = "SendNex";

    /// <summary>Token lifetime in minutes.</summary>
    public int TokenLifetimeMinutes { get; set; } = 30;

    /// <summary>Max forgot-password requests per email per hour.</summary>
    public int MaxRequestsPerEmailPerHour { get; set; } = 3;

    /// <summary>Max forgot-password requests per IP per hour.</summary>
    public int MaxRequestsPerIpPerHour { get; set; } = 3;
}
