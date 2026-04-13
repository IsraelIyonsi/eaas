using EaaS.Api.Constants;

namespace EaaS.Api.Features.Admin;

internal static class AdminEndpointHelpers
{
    internal static Guid GetAdminUserId(HttpContext httpContext)
        => Guid.Parse(httpContext.User.FindFirst(ClaimNameConstants.AdminUserId)?.Value ?? Guid.Empty.ToString());
}
