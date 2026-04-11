# CQRS & Endpoint Patterns

## Commands & Queries

- **Command**: `sealed record` implementing `IRequest<TResult>`
- **Query**: `sealed record` implementing `IRequest<TResult>`
- **Handler**: `sealed class` implementing `IRequestHandler<TRequest, TResult>`
- **Validator**: `sealed class` extending `AbstractValidator<TRequest>`
- **Result**: `sealed record` — enum values ALWAYS `string`, never enum type

**Critical rules:**
- Enum values in result DTOs converted via `.ToString()`
- Query handlers always use `.AsNoTracking()`
- Use `Guid.NewGuid()` for IDs, `DateTime.UtcNow` for timestamps

## Endpoint Pattern (Minimal API)

Static class with static `Map` method:

```csharp
public static class CreateInboundRuleEndpoint
{
    public sealed record CreateInboundRuleRequest(...);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (request, httpContext, mediator) => { ... })
        .WithName("CreateInboundRule")
        .WithOpenApi()
        .Produces<ApiResponse<Result>>(201)
        .Produces<ApiErrorResponse>(400);
    }
}
```

**Rules:**
- Routes from `RouteConstants`, tags from `TagConstants`
- `.WithName()` and `.WithOpenApi()` on every endpoint
- Success: `ApiResponse.Ok(data)`, Error: `ApiErrorResponse.Create(code, message)`
- POST → `Results.Created(...)`, GET → `Results.Ok(...)`

## Endpoint Registration

In `EndpointMappingExtensions.cs`:
```csharp
var group = app.MapGroup(RouteConstants.Feature)
    .RequireAuthorization()
    .WithTags(TagConstants.Feature);
FeatureEndpoint.Map(group);
```
