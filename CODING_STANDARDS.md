# EaaS Coding Standards

The definitive reference for all development conventions in this codebase. Every new feature, bug fix, and refactor must follow these patterns exactly.

---

## 1. Backend (.NET) Standards

### 1.1 Project Structure

The solution follows a Clean Architecture with vertical slice features:

```
src/
  EaaS.Domain/          # Entities, Enums, Exceptions, Interfaces (zero dependencies)
  EaaS.Shared/          # Constants, Models (ApiResponse, PagedResponse), Utilities
  EaaS.Infrastructure/  # EF Core, Redis, MassTransit, AWS services
  EaaS.Api/             # Minimal API endpoints, MediatR handlers, validators
  EaaS.Worker/          # Background consumer host
  EaaS.WebhookProcessor/ # SES webhook ingestion
  EaaS.Dashboard/       # (legacy — dashboard is now Next.js in dashboard/)
```

**Feature folder organization (vertical slices):**

```
EaaS.Api/Features/
  Inbound/
    Rules/
      CreateInboundRuleCommand.cs      # Command record
      CreateInboundRuleHandler.cs      # Handler class
      CreateInboundRuleValidator.cs    # FluentValidation
      CreateInboundRuleEndpoint.cs     # Minimal API endpoint
      ListInboundRulesQuery.cs         # Query record
      ListInboundRulesHandler.cs       # Query handler
      ListInboundRulesEndpoint.cs      # GET endpoint
      InboundRuleResult.cs             # Result DTO record
    Emails/
      GetInboundEmailEndpoint.cs
      ListInboundEmailsEndpoint.cs
```

**Where things go:**

| Artifact | Location |
|---|---|
| Entities | `EaaS.Domain/Entities/` |
| Enums | `EaaS.Domain/Enums/` |
| Interfaces | `EaaS.Domain/Interfaces/` |
| Domain exceptions | `EaaS.Domain/Exceptions/` |
| EF configurations | `EaaS.Infrastructure/Persistence/Configurations/` |
| Services (S3, SES, Redis) | `EaaS.Infrastructure/Services/` |
| MassTransit consumers | `EaaS.Infrastructure/Messaging/` |
| Message contracts | `EaaS.Infrastructure/Messaging/Contracts/` |
| Settings classes | `EaaS.Infrastructure/Configuration/` |
| Shared constants | `EaaS.Shared/Constants/` |
| API response models | `EaaS.Shared/Models/` (namespace: `EaaS.Shared.Contracts`) |
| Route/tag constants | `EaaS.Api/Constants/` |
| MediatR pipeline behaviors | `EaaS.Api/Behaviors/` |
| Auth handlers | `EaaS.Api/Authentication/` |
| Global middleware | `EaaS.Api/Middleware/` |

### 1.2 Entity Pattern

Entities are plain POCOs with no base class. Follow these conventions exactly:

```csharp
using EaaS.Domain.Enums;

namespace EaaS.Domain.Entities;

public class InboundEmail
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Required strings: default to string.Empty
    public string MessageId { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;

    // Optional strings: nullable
    public string? FromName { get; set; }
    public string? Subject { get; set; }

    // JSON columns: default to "[]" for arrays, "{}" for objects
    public string ToEmails { get; set; } = "[]";
    public string CcEmails { get; set; } = "[]";
    public string Metadata { get; set; } = "{}";

    // Array columns: default to Array.Empty<T>()
    public string[] Tags { get; set; } = Array.Empty<string>();

    // Enums: default to the initial status
    public InboundEmailStatus Status { get; set; } = InboundEmailStatus.Received;

    // Optional FK
    public Guid? OutboundEmailId { get; set; }

    // Timestamps
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties: null! for required, ? for optional
    public Tenant Tenant { get; set; } = null!;
    public Email? OutboundEmail { get; set; }
    public ICollection<InboundAttachment> Attachments { get; set; } = new List<InboundAttachment>();
}
```

**Rules:**
- No base class. No `IEntity` interface. Plain POCOs.
- `Guid Id` and `Guid TenantId` on every multi-tenant entity.
- Required navigation properties use `= null!` (EF will populate).
- Optional navigation properties use `?`.
- Collection navigation properties initialize to `new List<T>()`.
- `CreatedAt` on every entity. `UpdatedAt` where applicable.

### 1.3 EF Core Configuration Pattern

Every entity gets a dedicated configuration class:

```csharp
using EaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EaaS.Infrastructure.Persistence.Configurations;

public sealed class InboundAttachmentConfiguration : IEntityTypeConfiguration<InboundAttachment>
{
    public void Configure(EntityTypeBuilder<InboundAttachment> builder)
    {
        // Table name: snake_case
        builder.ToTable("inbound_attachments");

        builder.HasKey(a => a.Id);

        // Primary key: gen_random_uuid() default
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        // All columns: snake_case via .HasColumnName()
        builder.Property(a => a.InboundEmailId)
            .HasColumnName("inbound_email_id")
            .IsRequired();

        builder.Property(a => a.Filename)
            .HasColumnName("filename")
            .HasMaxLength(255)
            .IsRequired();

        // JSON columns: .HasColumnType("jsonb")
        builder.Property(e => e.ToEmails)
            .HasColumnName("to_emails")
            .HasColumnType("jsonb")
            .IsRequired();

        // JSON defaults: .HasDefaultValueSql("'[]'::jsonb") or "'{}'::jsonb"
        builder.Property(e => e.CcEmails)
            .HasColumnName("cc_emails")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        // Array columns: .HasColumnType("text[]")
        builder.Property(e => e.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'");

        // Timestamps
        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Indexes: .HasDatabaseName("idx_tablename_column")
        builder.HasIndex(a => a.InboundEmailId)
            .HasDatabaseName("idx_inbound_attachments_email");

        // Composite indexes
        builder.HasIndex(e => new { e.TenantId, e.ReceivedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_inbound_emails_tenant_received");

        // Partial indexes
        builder.HasIndex(e => e.InReplyTo)
            .HasFilter("in_reply_to IS NOT NULL")
            .HasDatabaseName("idx_inbound_emails_in_reply_to");
    }
}
```

**Enum Registration (DUAL requirement):**

Enums must be registered in TWO places:

1. `AppDbContext.OnModelCreating` -- for EF Core:
```csharp
modelBuilder.HasPostgresEnum<InboundEmailStatus>();
modelBuilder.HasPostgresEnum<InboundRuleAction>();
```

2. `DependencyInjection.cs` -- for NpgsqlDataSourceBuilder:
```csharp
dataSourceBuilder.MapEnum<InboundEmailStatus>();
dataSourceBuilder.MapEnum<InboundRuleAction>();
```

Missing either registration will cause runtime failures. Always add to both.

### 1.4 CQRS Pattern (Commands/Queries)

Every feature operation follows this exact structure:

**Command** -- sealed record implementing `IRequest<TResult>`:
```csharp
public sealed record CreateInboundRuleCommand(
    Guid TenantId,
    string Name,
    Guid DomainId,
    string MatchPattern,
    InboundRuleAction Action,
    string? WebhookUrl,
    string? ForwardTo,
    int Priority) : IRequest<InboundRuleResult>;
```

**Query** -- sealed record implementing `IRequest<TResult>`:
```csharp
public sealed record ListInboundRulesQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    Guid? DomainId) : IRequest<PagedResponse<InboundRuleResult>>;
```

**Handler** -- sealed class implementing `IRequestHandler<TRequest, TResult>`:
```csharp
public sealed class CreateInboundRuleHandler : IRequestHandler<CreateInboundRuleCommand, InboundRuleResult>
{
    private readonly AppDbContext _dbContext;

    public CreateInboundRuleHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InboundRuleResult> Handle(
        CreateInboundRuleCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate business rules (throw domain exceptions)
        // 2. Create entity
        // 3. Save
        // 4. Return result DTO
    }
}
```

**Validator** -- sealed class extending `AbstractValidator<TRequest>`:
```csharp
public sealed class CreateInboundRuleValidator : AbstractValidator<CreateInboundRuleCommand>
{
    public CreateInboundRuleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        // Conditional validation
        RuleFor(x => x.WebhookUrl)
            .NotEmpty().WithMessage("WebhookUrl is required when Action is Webhook.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("WebhookUrl must be a valid absolute URL.")
            .When(x => x.Action == InboundRuleAction.Webhook);
    }
}
```

**Result** -- sealed record with response fields:
```csharp
public sealed record InboundRuleResult(
    Guid Id,
    string Name,
    Guid DomainId,
    string DomainName,
    string MatchPattern,
    string Action,       // Always string, never enum
    string? WebhookUrl,
    string? ForwardTo,
    bool IsActive,
    int Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

**Critical rules:**
- Enum values in result DTOs are ALWAYS `string`, converted via `.ToString()`.
- Query handlers always use `.AsNoTracking()`.
- Use `Guid.NewGuid()` for new entity IDs.
- Use `DateTime.UtcNow` for all timestamps.

### 1.5 Endpoint Pattern (Minimal API)

Every endpoint is a static class with a static `Map` method:

```csharp
public static class CreateInboundRuleEndpoint
{
    // Request DTO (nested in the endpoint class)
    public sealed record CreateInboundRuleRequest(
        string Name,
        Guid DomainId,
        string MatchPattern,
        string Action,         // String from client, parse to enum in endpoint
        string? WebhookUrl,
        string? ForwardTo,
        int Priority);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            CreateInboundRuleRequest request,
            HttpContext httpContext,
            IMediator mediator) =>
        {
            // Extract TenantId from auth claims
            var tenantId = GetTenantId(httpContext);

            // Parse enum strings
            if (!Enum.TryParse<InboundRuleAction>(request.Action, ignoreCase: true, out var action))
                return Results.BadRequest(ApiErrorResponse.Create(
                    "VALIDATION_ERROR",
                    $"Invalid action '{request.Action}'."));

            var command = new CreateInboundRuleCommand(
                tenantId, request.Name, request.DomainId, ...);
            var result = await mediator.Send(command);

            return Results.Created(
                $"/api/v1/inbound/rules/{result.Id}",
                ApiResponse.Ok(result));
        })
        .WithName("CreateInboundRule")        // Required
        .WithSummary("Create an inbound rule")
        .WithDescription("Creates a new inbound email routing rule.")
        .WithOpenApi()                         // Required
        .Produces<ApiResponse<InboundRuleResult>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("TenantId")?.Value;
        return tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
    }
}
```

**Rules:**
- Routes come from `RouteConstants` -- NEVER hardcoded paths.
- Tags come from `TagConstants`.
- `.WithName()` and `.WithOpenApi()` on every endpoint.
- Success: `ApiResponse.Ok(data)`.
- Error: `ApiErrorResponse.Create(code, message)`.
- `GetTenantId()` is a private static helper in each endpoint class.
- POST returns `Results.Created(...)`, GET returns `Results.Ok(...)`.

### 1.6 Endpoint Registration

All endpoints are registered in `EndpointMappingExtensions.cs`:

```csharp
public static WebApplication MapApiEndpoints(this WebApplication app)
{
    // Group per feature with shared route prefix, auth, and tags
    var inboundRulesGroup = app.MapGroup(RouteConstants.InboundRules)
        .RequireAuthorization()
        .WithTags(TagConstants.InboundRules);

    CreateInboundRuleEndpoint.Map(inboundRulesGroup);
    ListInboundRulesEndpoint.Map(inboundRulesGroup);
    GetInboundRuleEndpoint.Map(inboundRulesGroup);
    UpdateInboundRuleEndpoint.Map(inboundRulesGroup);
    DeleteInboundRuleEndpoint.Map(inboundRulesGroup);

    return app;
}
```

### 1.7 Constants

**Route constants** (`EaaS.Api/Constants/RouteConstants.cs`):
```csharp
public static class RouteConstants
{
    private const string ApiBase = "/api/v1";
    public const string InboundRules = $"{ApiBase}/inbound/rules";
    public const string InboundEmails = $"{ApiBase}/inbound/emails";
}
```

**Tag constants** (`EaaS.Api/Constants/TagConstants.cs`):
```csharp
public static class TagConstants
{
    public const string InboundRules = "Inbound Rules";
    public const string InboundEmails = "Inbound Emails";
}
```

**Shared constants** (`EaaS.Shared/Constants/`):
```csharp
// MessagingConstants.cs - queue names
public const string InboundEmailProcessQueue = "eaas-inbound-process";

// PaginationConstants.cs - page size limits
// CacheConstants.cs - Redis key prefixes and TTLs
// RateLimitConstants.cs - rate limit thresholds
```

### 1.8 Service Registration

`Program.cs` stays thin -- only calls extension methods:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);     // DB, Redis, MassTransit
builder.Services.AddEmailProvider(builder.Configuration);       // SES or SMTP
builder.Services.AddInboundServices(builder.Configuration);     // S3 storage, MIME parser
builder.Services.AddApiServices(builder.Configuration);         // MediatR, auth, Swagger

var app = builder.Build();
app.UseApiMiddleware();    // Swagger, auth, health checks
app.MapApiEndpoints();     // All feature endpoints
```

**Service lifetimes:**
- `Singleton` for stateless services: Redis, S3, SES, tracking token service.
- `Scoped` for DB-dependent services: TrackingPixelInjector, SuppressionChecker.
- `Transient` for pipeline behaviors: `ValidationBehavior<,>`.

### 1.9 API Response Models

All API responses use these contracts from `EaaS.Shared.Contracts`:

```csharp
// Success
public record ApiResponse<T>(bool Success, T Data);
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => new(true, data);
}

// Error
public record ApiErrorResponse(bool Success, ApiError Error)
{
    public static ApiErrorResponse Create(
        string code, string message, List<ErrorDetail>? details = null)
        => new(false, new ApiError(code, message, details));
}

// Pagination
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
```

### 1.10 Messaging (MassTransit)

**Message contract** -- sealed record in `Messaging/Contracts/`:
```csharp
public sealed record ProcessInboundEmailMessage
{
    public string S3BucketName { get; init; } = string.Empty;
    public string S3ObjectKey { get; init; } = string.Empty;
    public string SesMessageId { get; init; } = string.Empty;
    public string[] Recipients { get; init; } = Array.Empty<string>();
    public string? SpamVerdict { get; init; }
}
```

**Consumer** -- sealed partial class implementing `IConsumer<TMessage>`:
```csharp
public sealed partial class InboundEmailConsumer : IConsumer<ProcessInboundEmailMessage>
{
    private readonly AppDbContext _dbContext;
    private readonly IInboundEmailStorage _storage;
    private readonly ILogger<InboundEmailConsumer> _logger;

    public async Task Consume(ConsumeContext<ProcessInboundEmailMessage> context)
    {
        var message = context.Message;
        LogReceivedMessage(_logger, message.SesMessageId);
        // ... processing logic ...
    }

    // [LoggerMessage] source generators for structured logging
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Received inbound email: SesMessageId={SesMessageId}")]
    private static partial void LogReceivedMessage(ILogger logger, string sesMessageId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to process: EmailId={EmailId}")]
    private static partial void LogProcessingFailed(ILogger logger, Guid emailId, Exception ex);
}
```

**Registration** in `MassTransitConfiguration.cs`:
```csharp
bus.AddConsumer<InboundEmailConsumer>();

cfg.ReceiveEndpoint(MessagingConstants.InboundEmailProcessQueue, e =>
{
    e.PrefetchCount = settings.InboundPrefetchCount;
    e.UseConcurrencyLimit(settings.InboundConcurrency);

    e.UseCircuitBreaker(cb => { ... });
    e.UseDelayedRedelivery(r => r.Intervals(...));
    e.UseMessageRetry(r => {
        r.Exponential(5, ...);
        r.Ignore<ValidationException>();
    });

    e.ConfigureConsumer<InboundEmailConsumer>(context);
});
```

**Rules:**
- Queue names come from `MessagingConstants` -- NEVER hardcoded.
- Always `sealed partial class` for `[LoggerMessage]` source generators.
- Always circuit breaker + delayed redelivery + message retry on every endpoint.
- `r.Ignore<ValidationException>()` on every retry config.
- Re-throw exceptions after logging so MassTransit retry handles them.

### 1.11 Error Handling

**Domain exceptions** extend `DomainException`:
```csharp
public abstract class DomainException : Exception
{
    public abstract int StatusCode { get; }
    public abstract string ErrorCode { get; }
}

public class NotFoundException : DomainException
{
    public override int StatusCode => 404;
    public override string ErrorCode => "NOT_FOUND";
    public NotFoundException(string message) : base(message) { }
}
```

**GlobalExceptionHandler** maps exceptions to HTTP responses:
```csharp
var (statusCode, errorResponse) = exception switch
{
    ValidationException validationEx => (400, ApiErrorResponse.Create(
        "VALIDATION_ERROR", "One or more validation errors occurred.",
        validationEx.Errors.Select(e => new ErrorDetail(e.PropertyName, e.ErrorMessage)).ToList())),

    DomainException domainEx => (domainEx.StatusCode,
        ApiErrorResponse.Create(domainEx.ErrorCode, domainEx.Message)),

    UnauthorizedAccessException => (401,
        ApiErrorResponse.Create("UNAUTHORIZED", exception.Message)),

    _ => (500, ApiErrorResponse.Create("INTERNAL_ERROR", "An unexpected error occurred."))
};
```

### 1.12 SQL Migrations

Migration files go in `scripts/migrate_sprintN.sql`:

```sql
-- Sprint 3 Migration: Webhook delivery logs table
-- Date: 2026-03-27

BEGIN;

CREATE TABLE IF NOT EXISTS webhook_delivery_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    webhook_id UUID NOT NULL REFERENCES webhooks(id) ON DELETE CASCADE,
    email_id UUID NOT NULL REFERENCES emails(id) ON DELETE CASCADE,
    event_type VARCHAR(50) NOT NULL,
    status_code INTEGER NOT NULL DEFAULT 0,
    success BOOLEAN NOT NULL DEFAULT false,
    error_message VARCHAR(2000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_webhook_delivery_logs_webhook
    ON webhook_delivery_logs(webhook_id);

COMMIT;
```

**Rules:**
- Wrap in `BEGIN`/`COMMIT`.
- `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`.
- Enum types: `CREATE TYPE ... AS ENUM` with lowercase values.
- PostgreSQL reserved words must be quoted (e.g., `"references"`).
- Index naming: `idx_tablename_column`.
- All primary keys: `UUID DEFAULT gen_random_uuid()`.
- All timestamps: `TIMESTAMPTZ NOT NULL DEFAULT NOW()`.

### 1.13 Testing (.NET)

**Test class structure** -- xUnit + FluentAssertions + NSubstitute:

```csharp
public sealed class CreateInboundRuleHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CreateInboundRuleHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateInboundRuleHandlerTests()
    {
        _dbContext = DbContextFactory.Create();
        _sut = new CreateInboundRuleHandler(_dbContext);
    }

    [Fact]
    public async Task Should_CreateRule_WhenValid()
    {
        var domain = SeedDomain();
        var command = TestDataBuilders.CreateInboundRule()
            .WithTenantId(_tenantId)
            .WithDomainId(domain.Id)
            .WithName("Support Catch-All")
            .Build();

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Support Catch-All");

        var stored = await _dbContext.InboundRules.FindAsync(result.Id);
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_WhenDomainDoesNotExist()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithDomainId(Guid.NewGuid())
            .Build();

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Domain not found*");
    }

    public void Dispose() => _dbContext.Dispose();
}
```

**DbContextFactory** -- in-memory database for unit tests:
```csharp
public static class DbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```

**TestDataBuilders** -- fluent builder pattern:
```csharp
public static class TestDataBuilders
{
    public static CreateInboundRuleCommandBuilder CreateInboundRule() => new();

    public sealed class CreateInboundRuleCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private string _name = "Catch-All Rule";
        private Guid _domainId = Guid.NewGuid();
        private string _matchPattern = "*@";
        private InboundRuleAction _action = InboundRuleAction.Store;

        public CreateInboundRuleCommandBuilder WithTenantId(Guid id) { _tenantId = id; return this; }
        public CreateInboundRuleCommandBuilder WithName(string name) { _name = name; return this; }
        // ... more WithXxx methods ...

        public CreateInboundRuleCommand Build() => new(
            _tenantId, _name, _domainId, _matchPattern, _action, ...);
    }
}
```

**Validator tests** -- use `FluentValidation.TestHelper`:
```csharp
public sealed class CreateInboundRuleValidatorTests
{
    private readonly CreateInboundRuleValidator _sut = new();

    [Fact]
    public void Should_Pass_WhenValid()
    {
        var command = TestDataBuilders.CreateInboundRule().Build();
        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_WhenNameEmpty()
    {
        var command = TestDataBuilders.CreateInboundRule()
            .WithName(string.Empty)
            .Build();
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }
}
```

**Rules:**
- Sealed test classes implementing `IDisposable`.
- Name: `Should_ExpectedBehavior_WhenCondition`.
- Handler tests: test success + each failure case.
- Validator tests: test valid + each invalid field.
- Use `TestDataBuilders` for all test data -- never raw constructors.
- `_sut` (system under test) naming convention.

---

## 2. Frontend (Next.js/React) Standards

### 2.1 Architecture

```
dashboard/src/
  app/                     # Next.js App Router pages
  components/
    shared/                # Reusable: PageHeader, DataTable, FilterBar, EmptyState, etc.
    ui/                    # shadcn/ui primitives (do not modify)
    {feature}/             # Feature-specific: inbound/, emails/, domains/
  lib/
    api/
      client.ts            # HttpClient base class + CrudRepository
      repositories/        # Feature repositories
    hooks/                 # React Query hooks per feature
    constants/             # Routes, API paths, query keys, status configs, UI values
    utils/
      api-response.ts      # extractItems, extractTotalCount, safeConfigLookup
  types/                   # TypeScript interfaces per feature domain
  e2e/                     # Playwright E2E tests
```

### 2.2 HTTP Client & Repository Pattern

**Base client** (`lib/api/client.ts`):
```typescript
export class HttpClient {
  protected async request<T>(method: string, path: string, body?: unknown,
    params?: Record<string, string>): Promise<T> {
    const url = new URL(`/api/proxy${path}`, window.location.origin);
    // ... fetch with credentials: 'include' ...
    const json = await res.json();
    return json.data ?? json;  // Unwraps ApiResponse envelope
  }

  protected get<T>(path: string, params?: Record<string, string>): Promise<T> { ... }
  protected post<T>(path: string, body?: unknown): Promise<T> { ... }
  protected put<T>(path: string, body?: unknown): Promise<T> { ... }
  protected del(path: string): Promise<void> { ... }
}
```

**CRUD Repository** -- for standard entities:
```typescript
export abstract class CrudRepository<TEntity, TCreateRequest, TUpdateRequest> extends HttpClient {
  protected abstract readonly basePath: string;
  async list(params?: Record<string, string>): Promise<PaginatedResponse<TEntity>> { ... }
  async getById(id: string): Promise<TEntity> { ... }
  async create(data: TCreateRequest): Promise<TEntity> { ... }
  async update(id: string, data: TUpdateRequest): Promise<TEntity> { ... }
  async remove(id: string): Promise<void> { ... }
}
```

**Feature repository** -- extends CrudRepository for standard CRUD, HttpClient for custom:
```typescript
// Standard CRUD -- just set basePath
export class InboundRuleRepository extends CrudRepository<
  InboundRule, CreateInboundRuleRequest, UpdateInboundRuleRequest
> {
  protected readonly basePath = ApiPaths.INBOUND_RULES;
}

// Custom operations -- extend HttpClient directly
export class InboundEmailRepository extends HttpClient {
  async list(params?: InboundEmailListParams): Promise<PaginatedResponse<InboundEmail>> {
    const queryParams: Record<string, string> = {};
    if (params?.status) queryParams.status = params.status;
    return this.get<PaginatedResponse<InboundEmail>>(ApiPaths.INBOUND_EMAILS, queryParams);
  }
}
```

### 2.3 React Query Hooks

Every repository gets a corresponding hook file:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';

// List hook with params
export function useInboundEmails(params?: InboundEmailListParams) {
  return useQuery({
    queryKey: QueryKeys.inboundEmails.list(params as Record<string, unknown>),
    queryFn: () => repositories.inboundEmail.list(params),
    staleTime: STALE_TIME_MS,
  });
}

// Detail hook with enabled guard
export function useInboundEmail(id: string | undefined) {
  return useQuery({
    queryKey: QueryKeys.inboundEmails.detail(id!),
    queryFn: () => repositories.inboundEmail.getById(id!),
    enabled: !!id,
  });
}

// Mutation with cache invalidation
export function useCreateInboundRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateInboundRuleRequest) =>
      repositories.inboundRule.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.inboundRules.all });
    },
  });
}
```

**Rules:**
- `staleTime: STALE_TIME_MS` on list/detail queries.
- `enabled: !!id` on detail queries with optional ID.
- Mutations invalidate the `all` key for the feature.
- Query keys come from `QueryKeys` constant -- NEVER hardcoded strings.

### 2.4 Constants

**All constants live in `lib/constants/`** -- no hardcoded values anywhere in components.

**API paths** (`api-paths.ts`):
```typescript
export const ApiPaths = {
  INBOUND_EMAILS: '/api/v1/inbound/emails',
  INBOUND_EMAIL_BY_ID: (id: string) => `/api/v1/inbound/emails/${id}`,
  INBOUND_RULES: '/api/v1/inbound/rules',
} as const;
```

**Query keys** (`query-keys.ts`):
```typescript
export const QueryKeys = {
  inboundEmails: {
    all: ['inbound-emails'] as const,
    list: (params?: Record<string, unknown>) => ['inbound-emails', 'list', params] as const,
    detail: (id: string) => ['inbound-emails', 'detail', id] as const,
  },
} as const;
```

**Page routes** (`routes.ts`):
```typescript
export const Routes = {
  INBOUND_EMAILS: '/inbound/emails',
  INBOUND_EMAIL_DETAIL: (id: string) => `/inbound/emails/${id}`,
  INBOUND_RULES: '/inbound/rules',
} as const;
```

**Status configs** (`status.ts`) -- display metadata for every enum:
```typescript
export const InboundEmailStatusConfig = {
  received: { label: 'Received', color: 'bg-blue-500', textColor: 'text-blue-400' },
  processing: { label: 'Processing', color: 'bg-amber-500', textColor: 'text-amber-400' },
  processed: { label: 'Processed', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  failed: { label: 'Failed', color: 'bg-red-500', textColor: 'text-red-400' },
} as const;
```

**UI constants** (`ui.ts`):
```typescript
export const PAGE_SIZE_DEFAULT = 20;
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
export const STALE_TIME_MS = 30_000;
export const REFETCH_INTERVAL_MS = 60_000;
```

### 2.5 Type Definitions

One file per feature domain in `types/`:

```typescript
// types/inbound.ts
export type InboundEmailStatus = 'received' | 'processing' | 'processed' | 'forwarded' | 'failed';
export type InboundRuleAction = 'webhook' | 'forward' | 'store';

export interface InboundEmail {
  id: string;
  messageId: string;
  fromEmail: string;
  fromName?: string;
  toEmails: Array<{ email: string; name?: string }>;
  ccEmails: Array<{ email: string; name?: string }>;
  subject?: string;
  status: InboundEmailStatus;
  attachments: InboundAttachment[];
  receivedAt: string;
  createdAt: string;
}

// Paginated response shape
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
```

**Rules:**
- Use `camelCase` for all fields -- must match .NET JSON serialization.
- Status enums are lowercase string unions (backend converts via `.ToString().ToLowerInvariant()`).
- Dates are `string` (ISO 8601 from backend).
- Optional fields use `?`.
- Paginated responses: `{ items, totalCount, page, pageSize }`.
- Some endpoints return flat arrays (domains, API keys) -- both shapes must be handled.

### 2.6 Generic API Response Utilities

All list data access goes through these helpers (`lib/utils/api-response.ts`):

```typescript
// Safely extracts items from either PaginatedResponse or flat array
export function extractItems<T>(
  data: T[] | PaginatedResponse<T> | undefined | null,
): T[] {
  if (!data) return [];
  if (Array.isArray(data)) return data;
  if ("items" in data && Array.isArray(data.items)) return data.items;
  return [];
}

// Safely extracts total count
export function extractTotalCount<T>(
  data: T[] | PaginatedResponse<T> | undefined | null,
): number {
  if (!data) return 0;
  if (Array.isArray(data)) return data.length;
  if ("totalCount" in data) return data.totalCount;
  return 0;
}

// Safely looks up config by key with fallback
export function safeConfigLookup<TConfig extends Record<string, unknown>>(
  config: TConfig,
  key: string | undefined | null,
  fallback: TConfig[keyof TConfig],
): TConfig[keyof TConfig] {
  if (!key) return fallback;
  return (config as Record<string, unknown>)[key] ?? fallback;
}
```

**NEVER** inline `Array.isArray` checks -- always use `extractItems()`.

### 2.7 Component Pattern

**Shared components** in `components/shared/`:

| Component | Purpose |
|---|---|
| `PageHeader` | Title + description + optional badge + action button + back link |
| `DataTable` | Generic table with pagination, loading skeletons, empty state, row selection |
| `FilterBar` | Search input + filter dropdowns + clear button |
| `EmptyState` | Icon + title + description + CTA button |
| `ConfirmDialog` | Destructive action confirmation |
| `CopyButton` | Copy-to-clipboard with feedback |
| `StatusBadge` | Colored badge from status config |
| `LoadingSkeleton` | Shimmer loading state |

**Page pattern** -- thin pages that compose hooks + shared components:

```tsx
"use client";

import { useState } from "react";
import { Inbox } from "lucide-react";
import { extractItems } from "@/lib/utils/api-response";
import { PageHeader } from "@/components/shared/page-header";
import { FilterBar } from "@/components/shared/filter-bar";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { useInboundEmails } from "@/lib/hooks/use-inbound";
import { Routes } from "@/lib/constants/routes";
import { PAGE_SIZE_DEFAULT } from "@/lib/constants/ui";

export default function InboundEmailsPage() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");

  const { data, isLoading } = useInboundEmails({
    page,
    page_size: PAGE_SIZE_DEFAULT,
    status: (status || undefined) as InboundEmailStatus | undefined,
  });

  const emails = extractItems(data);
  const total = data?.totalCount ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader title="Received Emails" badge={total > 0 ? `${total}` : undefined} />
      <FilterBar search={{ ... }} filters={[ ... ]} onClear={handleClearFilters} />
      <DataTable<InboundEmail>
        columns={columns}
        data={emails}
        total={total}
        loading={isLoading}
        emptyState={
          <EmptyState icon={Inbox} title="No inbound emails yet" description="..." />
        }
      />
    </div>
  );
}
```

**Rules:**
- `"use client"` on every page that uses hooks.
- Pages are thin -- hooks + shared components + layout. No business logic.
- Feature-specific rendering goes in `components/{feature}/`.
- Every page handles loading, empty, and error states.

### 2.8 Styling

- **Tailwind CSS** with CSS variable-based theming.
- Use semantic tokens: `bg-background`, `text-foreground`, `bg-muted`, `text-muted-foreground`, `border-border`.
- **NO hardcoded hex colors** (`bg-[#...]`) in components -- use theme variables.
- Status badge colors use Tailwind utilities from status config objects.
- Primary color: `#2563eb` (blue-600).
- Fonts: Inter (UI), JetBrains Mono (code).
- Sidebar: always dark (`#0f172a`) in both light/dark modes.
- shadcn/ui components in `components/ui/` -- do not modify these directly.

### 2.9 API Response Handling

Critical patterns that prevent bugs:

1. **Enum values** are returned as **lowercase strings** from the backend (`.ToString().ToLowerInvariant()` or `.ToString()` for PascalCase enums like DomainStatus).
2. **JSON columns** (toEmails, ccEmails, headers) must be **parsed** in the API endpoint's `Select()` projection before returning to the frontend.
3. **Enum values** are returned as **integers** by default from EF Core -- ALWAYS convert to string in the endpoint's `Select()`.
4. Use `extractItems()` for all list data -- NEVER inline `Array.isArray` checks.
5. Use `safeConfigLookup()` for status display -- handles unknown values gracefully.

### 2.10 E2E Testing (Playwright)

**Test structure:**
```typescript
import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Inbound Emails Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);    // Sets up mock API + authenticates
    await page.goto("/inbound/emails");
  });

  test("should display received emails list with data", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Received Emails" })
    ).toBeVisible();

    const tableRows = page.locator("table tbody tr");
    await expect(tableRows.first()).toBeVisible({ timeout: 10000 });
  });

  test("should show empty state when no inbound emails", async ({ page }) => {
    // Override specific route for this test
    await page.route("**/api/proxy/**", async (route) => {
      const url = route.request().url();
      if (url.includes("/api/v1/inbound/emails")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            success: true,
            data: { items: [], totalCount: 0, page: 1, pageSize: 10 },
          }),
        });
      }
      return route.fallback();
    });
    await page.reload();
    await expect(page.getByText("No inbound emails yet")).toBeVisible();
  });
});
```

**Mock API** (`e2e/helpers/mock-api.ts`):
- Intercepts all `**/api/proxy/**` routes.
- Returns mock data matching the exact `ApiResponse<T>` envelope.
- Field names use camelCase matching TypeScript types.
- Paginated responses wrap data in `{ items, totalCount, page, pageSize }`.

**Auth helper** (`e2e/helpers/auth.ts`):
```typescript
export async function login(page: Page) {
  await setupMockApi(page);
  await page.goto("/login");
  await page.getByLabel("Username").fill("admin");
  await page.getByLabel("Password").fill("admin");
  await page.getByRole("button", { name: "Sign In" }).click();
  await page.waitForURL("/", { timeout: 10000 });
}
```

**Rules:**
- Use `getByRole`, `getByText`, `getByLabel` -- NOT CSS selectors.
- Mock data field names MUST match actual API response (camelCase).
- Every new page needs a test file in `dashboard/e2e/`.
- Override routes per-test with `route.fallback()` for non-matching URLs.

---

## 3. Infrastructure Standards

### 3.1 Docker

```
docker-compose.yml           # Base services (postgres, redis, rabbitmq, api, worker)
docker-compose.override.yml  # Local dev overrides (ports, Mailpit, env vars)
docker-compose.prod.yml      # Production scaling
docker-compose.prod.env      # Production environment variables
```

- Environment variables for ALL secrets -- NEVER hardcode.
- `docker-compose.override.yml` auto-loads for local dev.

### 3.2 Database

- PostgreSQL with Npgsql.
- Table partitioning for high-volume tables (emails, inbound_emails, email_events).
- PgBouncer for connection pooling in production.
- Enum types registered via `HasPostgresEnum` and `MapEnum` (dual registration).
- `EnableRetryOnFailure(maxRetryCount: 3)` on the connection.

---

## 4. Review Checklist (for every PR)

Before marking any work as done, verify ALL of these:

### Backend
- [ ] `dotnet build`: 0 errors, 0 warnings
- [ ] `dotnet test`: all pass
- [ ] New enum registered in BOTH `AppDbContext.OnModelCreating` AND `DependencyInjection.cs`
- [ ] Status enums returned as strings in result DTOs, not integers
- [ ] JSON columns parsed before returning from API endpoints
- [ ] Route paths from `RouteConstants`, tags from `TagConstants`
- [ ] `.WithName()` and `.WithOpenApi()` on every endpoint
- [ ] `ApiResponse.Ok()` for success, `ApiErrorResponse.Create()` for errors
- [ ] Handler tests cover success + each failure case
- [ ] Validator tests cover valid + each invalid field
- [ ] TestDataBuilders used for all test data

### Frontend
- [ ] `npx next build`: 0 errors
- [ ] `npx playwright test`: all pass
- [ ] No hardcoded strings (routes, API paths, query keys, colors)
- [ ] New types match actual API response shape (verify with curl)
- [ ] `extractItems()` used for list data -- never inline `Array.isArray`
- [ ] `safeConfigLookup()` used for status display
- [ ] New pages handle loading, empty, and error states
- [ ] `"use client"` on pages using hooks
- [ ] Status configs added for any new enum values
- [ ] Query keys defined in `QueryKeys` constant
- [ ] API paths defined in `ApiPaths` constant
- [ ] Page routes defined in `Routes` constant
- [ ] All constants referenced, not duplicated
- [ ] New page has Playwright test in `e2e/`
