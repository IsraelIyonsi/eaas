using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Admin.AuditLogs;

public static class ListAuditLogsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            IMediator mediator,
            int page = 1,
            int pageSize = 20,
            string? action = null,
            Guid? adminUserId = null,
            DateTime? from = null,
            DateTime? to = null) =>
        {
            var query = new ListAuditLogsQuery(page, pageSize, action, adminUserId, from, to);
            var result = await mediator.Send(query);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("ListAuditLogs")
        .WithSummary("List audit logs")
        .WithDescription("Returns paginated audit logs with optional filtering.")
        .Produces<ApiResponse<PagedResponse<AuditLogResult>>>(StatusCodes.Status200OK);
    }
}
