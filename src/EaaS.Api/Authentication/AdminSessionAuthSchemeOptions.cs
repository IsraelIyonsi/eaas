using Microsoft.AspNetCore.Authentication;

namespace EaaS.Api.Authentication;

public class AdminSessionAuthSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// HMAC-SHA256 secret used to verify admin session cookies.
    /// Must match the SESSION_SECRET used by the dashboard.
    /// </summary>
    public string SessionSecret { get; set; } = string.Empty;
}
