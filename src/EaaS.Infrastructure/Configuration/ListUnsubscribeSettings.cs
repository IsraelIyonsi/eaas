namespace EaaS.Infrastructure.Configuration;

/// <summary>
/// Configuration for RFC 8058 One-Click List-Unsubscribe header injection and footer.
/// See CAN-SPAM Act §7704(a)(5) and EU Directive 2002/58/EC Art 13.
/// </summary>
public sealed class ListUnsubscribeSettings
{
    public const string SectionName = "ListUnsubscribe";

    /// <summary>HMAC secret used to derive recipient tokens.</summary>
    public string HmacSecret { get; set; } = "";

    /// <summary>Public base URL used in the https unsubscribe variant, e.g. https://sendnex.xyz.</summary>
    public string BaseUrl { get; set; } = "https://sendnex.xyz";

    /// <summary>Mailto host used for the mailto: unsubscribe variant, e.g. sendnex.xyz.</summary>
    public string MailtoHost { get; set; } = "sendnex.xyz";
}
