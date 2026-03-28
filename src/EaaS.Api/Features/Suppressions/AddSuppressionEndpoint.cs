using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.Suppressions;

public static class AddSuppressionEndpoint
{
    public sealed record AddSuppressionRequest(string EmailAddress);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (AddSuppressionRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);

            var command = new AddSuppressionCommand(tenantId, request.EmailAddress);
            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/suppressions/{result.Id}", ApiResponse.Ok(result));
        })
        .WithName("AddSuppression")
        .WithSummary("Manually suppress an email address")
        .WithDescription("Adds an email address to the suppression list with 'manual' reason.")
        .WithOpenApi()
        .Produces<ApiResponse<AddSuppressionResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
