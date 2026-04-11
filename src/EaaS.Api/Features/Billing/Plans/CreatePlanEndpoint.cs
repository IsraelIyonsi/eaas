using EaaS.Domain.Enums;
using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Billing.Plans;

public static class CreatePlanEndpoint
{
    public sealed record CreatePlanRequest(
        string Name,
        PlanTier Tier,
        decimal MonthlyPriceUsd,
        decimal AnnualPriceUsd,
        int DailyEmailLimit,
        long MonthlyEmailLimit,
        int MaxApiKeys,
        int MaxDomains,
        int MaxTemplates,
        int MaxWebhooks,
        bool CustomDomainBranding,
        bool PrioritySupport);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreatePlanRequest request, IMediator mediator) =>
        {
            var command = new CreatePlanCommand(
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
                request.PrioritySupport);

            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/admin/billing/plans/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("CreatePlan")
        .WithSummary("Create a new billing plan")
        .WithDescription("Creates a new billing plan for the platform.")
        .Produces<ApiResponse<PlanResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }
}
