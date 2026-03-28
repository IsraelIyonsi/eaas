namespace EaaS.Infrastructure.Configuration;

public sealed class TrackingSettings
{
    public const string SectionName = "Tracking";

    public string HmacSecret { get; set; } = "default-tracking-secret-change-in-production";
    public string BaseUrl { get; set; } = "https://hooks.email.israeliyonsi.dev";
    public string FallbackRedirectUrl { get; set; } = "https://email.israeliyonsi.dev";
}
