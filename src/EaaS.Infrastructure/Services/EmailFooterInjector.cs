using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace EaaS.Infrastructure.Services;

/// <summary>
/// Injects the CAN-SPAM §7704(a)(5) compliant footer — physical postal
/// address plus a visible unsubscribe link — into the HTML and text bodies
/// of an outbound email.
/// </summary>
/// <remarks>
/// Registered as a singleton in DI. Members are intentionally instance methods
/// (not static) so the class can later gain configuration state (e.g. branded
/// footer templates per tenant) without a breaking signature change for callers.
/// </remarks>
[SuppressMessage("Performance", "CA1822:Mark members as static",
    Justification = "Kept as instance members so this DI-registered service can evolve to carry configuration state without breaking call sites.")]
public sealed class EmailFooterInjector
{
    public string InjectHtmlFooter(
        string? htmlBody,
        string tenantDisplayName,
        string postalAddress,
        string unsubscribeUrl)
    {
        var footer = BuildHtmlFooter(tenantDisplayName, postalAddress, unsubscribeUrl);

        if (string.IsNullOrWhiteSpace(htmlBody))
            return footer;

        var bodyClose = htmlBody.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyClose >= 0
            ? htmlBody.Insert(bodyClose, footer)
            : htmlBody + footer;
    }

    public string InjectTextFooter(
        string? textBody,
        string tenantDisplayName,
        string postalAddress,
        string unsubscribeUrl)
    {
        var footer = BuildTextFooter(tenantDisplayName, postalAddress, unsubscribeUrl);
        if (string.IsNullOrWhiteSpace(textBody))
            return footer;
        return textBody.TrimEnd() + "\n\n" + footer;
    }

    private static string BuildHtmlFooter(string displayName, string postalAddress, string url)
    {
        var escName = WebUtility.HtmlEncode(displayName ?? string.Empty);
        var escAddr = WebUtility.HtmlEncode(postalAddress ?? string.Empty).Replace("\n", "<br>");
        var escUrl = WebUtility.HtmlEncode(url);
        return $"<hr><p style=\"font-size:12px;color:#666\">You received this because you're on {escName}'s mailing list. <a href=\"{escUrl}\">Unsubscribe</a>. {escAddr}</p>";
    }

    private static string BuildTextFooter(string displayName, string postalAddress, string url)
    {
        return $"--\nYou received this because you're on {displayName}'s mailing list.\nUnsubscribe: {url}\n{postalAddress}";
    }
}
