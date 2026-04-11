namespace EaaS.Api.Features.Admin;

internal static class AdminEndpointHelpers
{
    internal static Guid GetAdminUserId(HttpContext httpContext)
        => Guid.Parse(httpContext.User.FindFirst("AdminUserId")?.Value ?? Guid.Empty.ToString());
}
