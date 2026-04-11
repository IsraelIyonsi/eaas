# Constants, Services, Responses, Errors & Messaging

## Constants

- Route constants: `EaaS.Api/Constants/RouteConstants.cs`
- Tag constants: `EaaS.Api/Constants/TagConstants.cs`
- Shared constants: `EaaS.Shared/Constants/` (messaging, pagination, cache, rate limits)

## Service Registration

`Program.cs` stays thin — only extension methods. Lifetimes:
- `Singleton`: stateless (Redis, S3, SES)
- `Scoped`: DB-dependent
- `Transient`: pipeline behaviors

## API Response Models (`EaaS.Shared.Contracts`)

- Success: `ApiResponse<T>(bool Success, T Data)` → `ApiResponse.Ok(data)`
- Error: `ApiErrorResponse` → `ApiErrorResponse.Create(code, message)`
- Pagination: `PagedResponse<T>(Items, Total, Page, PageSize, TotalPages)`

## Error Handling

Domain exceptions extend `DomainException` with `StatusCode` and `ErrorCode`. `GlobalExceptionHandler` maps them to HTTP responses.

## Messaging (MassTransit)

- Message contracts: `sealed record` in `Messaging/Contracts/`
- Consumers: `sealed partial class` implementing `IConsumer<T>`
- Use `[LoggerMessage]` source generators for structured logging
- Queue names from `MessagingConstants`
- Always: circuit breaker + delayed redelivery + message retry
- Always: `r.Ignore<ValidationException>()` on retry config
