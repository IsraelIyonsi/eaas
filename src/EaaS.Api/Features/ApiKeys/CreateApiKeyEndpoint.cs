using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public static class CreateApiKeyEndpoint
{
    public sealed record CreateApiKeyRequest(string Name);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateApiKeyRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            var tenantId = GetTenantId(httpContext);
            var command = new CreateApiKeyCommand(request.Name, tenantId);
            var result = await mediator.Send(command);

            return Results.Created($"/api/v1/keys/{result.Id}", ApiResponse.Ok(new
            {
                id = result.Id,
                name = result.Name,
                keyPrefix = result.KeyPrefix,
                key = result.Key,
                createdAt = result.CreatedAt
            }));
        })
        .WithName("CreateApiKey")
        .Produces<ApiResponse<object>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
