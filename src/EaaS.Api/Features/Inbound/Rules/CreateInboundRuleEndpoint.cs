using EaaS.Domain.Enums;
using EaaS.Shared.Contracts;
using MediatR;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Inbound.Rules;

public static class CreateInboundRuleEndpoint
{
    public sealed record CreateInboundRuleRequest(
        string Name,
        Guid DomainId,
        string MatchPattern,
        string Action,
        string? WebhookUrl,
        string? ForwardTo,
        int Priority);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateInboundRuleRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            if (!Enum.TryParse<InboundRuleAction>(request.Action, ignoreCase: true, out var action))
                return Results.BadRequest(ApiErrorResponse.Create("VALIDATION_ERROR", $"Invalid action '{request.Action}'. Must be one of: {string.Join(", ", Enum.GetNames<InboundRuleAction>())}"));

            var command = new CreateInboundRuleCommand(
                tenantId,
                request.Name,
                request.DomainId,
                request.MatchPattern,
                action,
                request.WebhookUrl,
                request.ForwardTo,
                request.Priority);

            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/inbound/rules/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("CreateInboundRule")
        .WithSummary("Create an inbound rule")
        .WithDescription("Creates a new inbound email routing rule.")
        .Produces<ApiResponse<InboundRuleResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
