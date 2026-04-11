using EaaS.Domain.Enums;
using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public static class UpdatePlanEndpoint
{
    public sealed record UpdatePlanRequest(
        string? Name,
        PlanTier? Tier,
        decimal? MonthlyPriceUsd,
        decimal? AnnualPriceUsd,
        int? DailyEmailLimit,
        long? MonthlyEmailLimit,
        int? MaxApiKeys,
        int? MaxDomains,
        int? MaxTemplates,
        int? MaxWebhooks,
        bool? CustomDomainBranding,
        bool? PrioritySupport,
        bool? IsActive);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, UpdatePlanRequest request, IMediator mediator) =>
        {
            var command = new UpdatePlanCommand(
                id,
                request.Name,
                request.Tier,
                request.MonthlyPriceUsd,
                request.AnnualPriceUsd,
                request.DailyEmailLimit,
                request.MonthlyEmailLimit,
                request.MaxApiKeys,
                request.MaxDomains,
                request.MaxTemplates,
                request.MaxWebhooks,
                request.CustomDomainBranding,
                request.PrioritySupport,
                request.IsActive);

            var result = await mediator.Send(command);

            return Results.Ok(ApiResponse.Ok(result));
        })
        .WithName("UpdatePlan")
        .WithSummary("Update a billing plan")
        .WithDescription("Updates an existing billing plan.")
        .Produces<ApiResponse<PlanResult>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }
}
