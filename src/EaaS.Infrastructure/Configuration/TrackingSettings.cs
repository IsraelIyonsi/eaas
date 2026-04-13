namespace EaaS.Infrastructure.Configuration;

public sealed class TrackingSettings
{
    public const string SectionName = "Tracking";

    public string HmacSecret { get; set; } = "";
    public string BaseUrl { get; set; } = "https://sendnex.xyz";
    public string FallbackRedirectUrl { get; set; } = "https://sendnex.xyz";
}
