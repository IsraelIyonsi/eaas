using EaaS.Domain.Enums;
using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Inbound.Rules;

public static class UpdateInboundRuleEndpoint
{
    public sealed record UpdateInboundRuleRequest(
        string? Name,
        string? MatchPattern,
        string? Action,
        string? WebhookUrl,
        string? ForwardTo,
        bool? IsActive,
        int? Priority);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdateInboundRuleRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            InboundRuleAction? action = null;
            if (request.Action is not null)
            {
                if (!Enum.TryParse<InboundRuleAction>(request.Action, ignoreCase: true, out var parsed))
                    return Results.BadRequest(ApiErrorResponse.Create("VALIDATION_ERROR", $"Invalid action '{request.Action}'. Must be one of: {string.Join(", ", Enum.GetNames<InboundRuleAction>())}"));
                action = parsed;
            }

            var command = new UpdateInboundRuleCommand(
                tenantId,
                id,
                request.Name,
                request.MatchPattern,
                action,
                request.WebhookUrl,
                request.ForwardTo,
                request.IsActive,
                request.Priority);

            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("UpdateInboundRule")
        .WithSummary("Update an inbound rule")
        .WithDescription("Updates an existing inbound rule. Partial updates are supported.")
        .Produces<ApiResponse<InboundRuleResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
