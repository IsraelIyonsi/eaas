using EaaS.Shared.Contracts;
using MediatR;

namespace EaaS.Api.Features.ApiKeys;

public static class CreateApiKeyEndpoint
{
    public sealed record CreateApiKeyRequest(string Name, Guid TenantId);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateApiKeyRequest request, IMediator mediator) =>
        {
            var command = new CreateApiKeyCommand(request.Name, request.TenantId);
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
        .WithOpenApi()
        .Produces<ApiResponse<object>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }
}
