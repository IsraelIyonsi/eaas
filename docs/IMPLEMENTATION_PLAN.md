# EaaS - Sprint 1 Implementation Plan (HOW)

**Author:** Senior Developer
**Date:** 2026-03-27
**Status:** Awaiting Gate 2 (Architect Review)
**Sprint:** 1 (MVP)
**Deadline:** 24 hours
**TDD Mandatory:** Yes -- failing tests first, then implementation

---

## Table of Contents

1. [Code Pattern Decisions](#1-code-pattern-decisions)
2. [Implementation Phases](#2-implementation-phases)
3. [Phase 1: Scaffolding (US-0.1) -- Target 2h](#3-phase-1-scaffolding)
4. [Phase 2: Data Layer (US-0.2, US-0.6) -- Target 2h](#4-phase-2-data-layer)
5. [Phase 3: Infrastructure (US-0.3, US-0.4, US-0.5) -- Target 2h](#5-phase-3-infrastructure)
6. [Phase 4: Core Features (US-5.1, US-5.3, US-3.1, US-3.2) -- Target 4h](#6-phase-4-core-features)
7. [Phase 5: Email Sending (US-1.1, US-2.1, US-2.2, US-1.3) -- Target 4h](#7-phase-5-email-sending)
8. [Phase 6: Testing & Polish -- Target 2h](#8-phase-6-testing--polish)
9. [Risk Mitigation](#9-risk-mitigation)
10. [Complete File Index](#10-complete-file-index)

---

## 1. Code Pattern Decisions

### 1.1 Dependency Injection

**Approach:** One `DependencyInjection.cs` extension method per project layer.

- `EaaS.Infrastructure` has `services.AddInfrastructure(IConfiguration)` that registers all repositories, EF Core, Redis, MassTransit, SES, Fluid, DNS, and the UnitOfWork.
- `EaaS.Api` has `services.AddApiServices()` that registers MediatR, FluentValidation validators (via assembly scanning), and middleware.
- Each project registers its own concerns. `Program.cs` calls both: `builder.Services.AddInfrastructure(config)` then `builder.Services.AddApiServices()`.

**Why:** Single responsibility per layer. Easy to find registrations. Standard .NET pattern.

### 1.2 Request Validation

**Approach:** FluentValidation with MediatR pipeline behavior.

- Each MediatR command/query has a corresponding `*Validator.cs` in the same feature folder.
- A `ValidationBehavior<TRequest, TResponse>` pipeline behavior intercepts every MediatR request, runs the validator, and throws `ValidationException` if invalid.
- The `GlobalExceptionHandler` catches `ValidationException` and maps it to a 400 response with the standard error envelope.
- Validators are registered via `services.AddValidatorsFromAssemblyContaining<Program>()`.

**Why:** Validation logic lives next to the handler (vertical slice). No manual validation calls. Consistent error format.

### 1.3 Error Handling

**Approach:** Global exception middleware (`GlobalExceptionHandler`) implementing `IExceptionHandler` (.NET 8).

- Maps domain exceptions to HTTP status codes:
  - `RecipientSuppressedException` -> 422 with code `RECIPIENT_SUPPRESSED`
  - `DomainNotVerifiedException` -> 422 with code `DOMAIN_NOT_VERIFIED`
  - `TemplateNotFoundException` -> 404 with code `NOT_FOUND`
  - `TemplateRenderException` -> 400 with code `VALIDATION_ERROR`
  - `DuplicateDomainException` -> 409 with code `CONFLICT`
  - `FluentValidation.ValidationException` -> 400 with code `VALIDATION_ERROR`
  - `UnauthorizedAccessException` -> 401 with code `UNAUTHORIZED`
  - All others -> 500 with code `INTERNAL_ERROR`
- Logs the full exception at Error level. Returns only the safe message to the client.
- No per-endpoint try/catch blocks.

**Why:** Centralized. Cannot forget error handling on a new endpoint. Consistent response format.

### 1.4 Response Format

**Approach:** Consistent envelope via `ApiResponse<T>` and `ApiErrorResponse` in `EaaS.Shared`.

```csharp
// Success
public record ApiResponse<T>(bool Success, T Data);

// Error
public record ApiErrorResponse(bool Success, ApiError Error);
public record ApiError(string Code, string Message, List<ErrorDetail>? Details = null);
public record ErrorDetail(string Field, string Message);
```

- Every endpoint returns `Results.Ok(new ApiResponse<T>(true, data))` or similar.
- Error responses are generated exclusively by `GlobalExceptionHandler`.
- `ApiResponse.Success(data)` and `ApiResponse.Error(code, message)` static factory methods for convenience.

**Why:** Matches the Architecture spec exactly. Single source of truth for envelope shape.

### 1.5 EF Core

**Approach:** Code-first with Fluent API configurations. Auto-migrate on startup in Development; CLI migration in Production.

- Each entity has a separate `IEntityTypeConfiguration<T>` class in `Infrastructure/Persistence/Configurations/`.
- Fluent API only (no data annotations on domain entities -- keeps Domain project clean).
- `DbContext.SaveChangesAsync` interceptor auto-sets `UpdatedAt` on modified entities.
- PostgreSQL enums mapped via `NpgsqlModelBuilderExtensions.HasPostgresEnum<T>()`.
- Snake_case column naming via Npgsql's `UseSnakeCaseNamingConvention()`.
- `IUnitOfWork` wraps `SaveChangesAsync` for explicit transaction commits.
- Connection string from `IConfiguration["ConnectionStrings:PostgreSQL"]`.

**Why:** Fluent API keeps domain entities POCO. Configurations are co-located and testable. Snake_case matches the DDL exactly.

### 1.6 MassTransit

**Approach:** MassTransit with RabbitMQ transport, declarative retry, and automatic topology.

- **Consumer pattern:** One consumer class per message type, implementing `IConsumer<T>`. Consumers live in the Worker project.
- **Retry configuration:** `UseDelayedRedelivery` with intervals [1m, 5m, 30m]. `UseMessageRetry` with 1 immediate retry for transient failures. After all retries exhausted, message goes to `_error` queue automatically.
- **Message contracts:** Message classes live in `Infrastructure/Messaging/Messages/`. They are plain C# records with no MassTransit-specific attributes. MassTransit serializes them as JSON.
- **Publishing:** `IMessagePublisher` (domain interface) implemented by `MassTransitPublisher` which injects `IBus` and calls `bus.Publish<T>(message)`.
- **Consumer registration:** `x.AddConsumer<SendEmailConsumer>()` in the MassTransit configuration. `cfg.ConfigureEndpoints(context)` auto-creates queues named after the consumer (e.g., `send-email`).
- **Concurrency:** `cfg.PrefetchCount = 10;` and `cfg.UseConcurrencyLimit(10);` per consumer.
- **No manual exchange/queue declarations.** MassTransit handles all topology.

**Why:** MassTransit eliminates 2-3 hours of manual RabbitMQ plumbing. Declarative retry is battle-tested. Consumer pattern is clean and testable.

### 1.7 AWS SES

**Approach:** SES v2 SDK (`AWSSDK.SimpleEmailV2`) abstracted behind `IEmailDeliveryService`.

- `SesEmailDeliveryService` implements `IEmailDeliveryService`. It creates `SendEmailRequest` with `RawMessage` content for full MIME control.
- `SesDomainIdentityService` handles `VerifyDomainIdentity` and `VerifyDomainDkim` calls. Not behind a domain interface (infrastructure-only concern called by the API handler directly through a concrete service registered in DI).
- `SesHealthCheck` calls `GetSendQuota` to verify connectivity and reports remaining quota.
- For testing: `IEmailDeliveryService` is mocked via NSubstitute. No actual SES calls in unit tests.
- SDK client created via `new AmazonSimpleEmailServiceV2Client(credentials, RegionEndpoint)` with credentials from `IOptions<SesOptions>`.

**Why:** Abstraction allows testing without AWS. RawMessage gives full MIME control for future attachments. v2 SDK is the current standard.

### 1.8 API Key Authentication

**Approach:** Custom `AuthenticationHandler<AuthenticationSchemeOptions>` registered as the default scheme.

- `ApiKeyAuthenticationHandler` extracts the Bearer token from the `Authorization` header.
- Computes SHA-256 hash of the token.
- Looks up the hash in the `api_keys` table (via `IApiKeyRepository.GetByKeyHashAsync`).
- If found and status is `active`: creates `ClaimsPrincipal` with claims for `tenant_id`, `api_key_id`, `allowed_domains`.
- If not found or revoked: returns 401.
- Updates `last_used_at` asynchronously (fire-and-forget, non-blocking).
- Redis cache: cache the hash-to-key mapping for 5 minutes to avoid DB lookup on every request. Invalidate on revoke.

**Why:** `AuthenticationHandler` integrates with ASP.NET Core's auth pipeline. Endpoints just use `[Authorize]` (or `.RequireAuthorization()`). Cleaner than custom middleware.

### 1.9 Rate Limiting

**Approach:** Redis-based sliding window via custom middleware.

- `RateLimitingMiddleware` extracts the `api_key_id` claim from the authenticated principal.
- Uses Redis sorted set: key = `ratelimit:{api_key_id}`, score = timestamp, member = unique request ID.
- ZREMRANGEBYSCORE to remove entries older than 1 second.
- ZCARD to count entries in the current window.
- If count >= 100: return 429 with `Retry-After` header.
- Otherwise: ZADD the current request and EXPIRE the key with TTL of 2 seconds.
- All Redis operations in a single Lua script for atomicity.

**Why:** Sliding window is more accurate than fixed window. Redis Lua script is atomic. Per-API-key as specified in the Architecture.

### 1.10 Logging

**Approach:** Serilog with compact JSON to console.

- **Sinks:** Console only (Docker captures stdout). Compact JSON format via `CompactJsonFormatter`.
- **Enrichers:** `FromLogContext`, `WithMachineName`, `WithProcessId`.
- **Correlation ID:** `RequestLoggingMiddleware` reads `X-Request-Id` header or generates a new GUID. Pushes to `Serilog.Context.LogContext`. Also passed as a MassTransit message header so Worker logs correlate.
- **Sensitive data exclusion:** API keys, email bodies, and passwords are never logged. Use `[LogMasked]` or explicit exclusion in log templates.
- **Request logging:** `RequestLoggingMiddleware` logs method, path, status code, duration. No request/response bodies.
- **Log levels:** `Information` for requests, `Warning` for validation failures, `Error` for exceptions, `Debug` for detailed flow.

**Why:** Compact JSON is machine-parseable for future log aggregation. Correlation ID enables tracing across API and Worker.

### 1.11 Testing

**Approach:** xUnit + FluentAssertions + NSubstitute (not Moq) + Testcontainers.

**Note on mocking library:** The Architecture lists Moq in the NuGet packages, but NSubstitute is a better choice. Moq has had trust/security concerns (version 4.20 SponsorLink incident). NSubstitute has a cleaner API and no controversial telemetry. I will use NSubstitute instead and update the package reference.

- **Unit tests:** All MediatR handlers tested with NSubstitute mocks for repositories and services. All validators tested for valid/invalid inputs. Located in `EaaS.Api.Tests` and `EaaS.Worker.Tests`.
- **Integration tests:** Repository tests use Testcontainers (PostgreSQL). Redis cache tests use Testcontainers (Redis). Located in `EaaS.Infrastructure.Tests`.
- **E2E tests:** Full API-to-DB flow using `WebApplicationFactory` with Testcontainers for all infrastructure. Located in `EaaS.Integration.Tests`.
- **Naming convention:** `MethodName_Scenario_ExpectedBehavior` (e.g., `Handle_ValidEmail_ReturnsQueuedStatus`).
- **TDD flow:** Write failing test -> implement minimum code to pass -> refactor. Every handler and validator gets tests first.

**Why:** TDD is mandatory per project rules. NSubstitute is safer and cleaner than Moq. Testcontainers provide real infrastructure for integration tests.

---

## 2. Implementation Phases -- Time Budget

| Phase | Hours | Stories | Points | Cumulative |
|-------|-------|---------|--------|------------|
| Phase 1: Scaffolding | 2.0h | US-0.1 | 5 | 5 |
| Phase 2: Data Layer | 2.0h | US-0.2, US-0.6 | 6 | 11 |
| Phase 3: Infrastructure | 2.0h | US-0.3, US-0.4, US-0.5 | 7 | 18 |
| Phase 4: Core Features | 4.0h | US-5.1, US-5.3, US-3.1, US-3.2 | 11 | 29 |
| Phase 5: Email Sending | 4.0h | US-1.1, US-2.1, US-2.2, US-1.3 | 16 | 45 |
| Phase 6: Testing & Polish | 2.0h | Integration tests, Docker verification | -- | 45 |
| **Total** | **16.0h** | **14 stories** | **45** | |

**Buffer:** 8 hours for debugging, Docker issues, AWS SES configuration, breaks, and unexpected blockers.

**Cut list (if behind schedule, drop in this order):**
1. US-2.2 (Update Template) -- 2 pts, saves ~45 min
2. US-1.3 (Template-based Send) -- 3 pts, saves ~1.5h
3. US-2.1 (Create Template) -- 3 pts, saves ~1h (only if US-1.3 also cut)
4. US-3.2 (Verify Domain DNS) -- 3 pts, saves ~1.5h
5. US-5.3 (Revoke API Key) -- 2 pts, saves ~30 min

**Absolute minimum MVP (32 points):** US-0.1, US-0.2, US-0.3, US-0.4, US-0.5, US-0.6, US-5.1, US-3.1, US-1.1

---

## 3. Phase 1: Scaffolding (US-0.1) -- Target 2h

### File #1: `eaas.sln`
- **Story:** US-0.1
- **What it does:** Solution file linking all projects.
- **Key patterns:** Created via `dotnet new sln`. Add all 7 src projects + 4 test projects.
- **Dependencies:** None (first file)
- **Test file:** N/A
- **Time:** 5 min

### File #2: `Directory.Build.props`
- **Story:** US-0.1
- **What it does:** Shared MSBuild properties for all projects -- target framework, nullable, implicit usings, warnings as errors.
- **Key patterns:**
  ```xml
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  ```
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #3: `Directory.Packages.props`
- **Story:** US-0.1
- **What it does:** Central NuGet package version management. Single source of truth for all package versions.
- **Key patterns:** `ManagePackageVersionsCentrally` + `CentralPackageTransitivePinningEnabled`. All `<PackageVersion>` entries as specified in Architecture section 9. Replace `Moq` with `NSubstitute` (version 5.1.0).
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 5 min

### File #4: `src/EaaS.Domain/EaaS.Domain.csproj`
- **Story:** US-0.1
- **What it does:** Domain project file. Zero NuGet dependencies.
- **Key patterns:** Class library. No `<PackageReference>` entries. No project references.
- **Dependencies:** Directory.Build.props, Directory.Packages.props
- **Test file:** N/A
- **Time:** 2 min

### File #5: `src/EaaS.Shared/EaaS.Shared.csproj`
- **Story:** US-0.1
- **What it does:** Shared project file. Zero NuGet dependencies.
- **Key patterns:** Class library. No `<PackageReference>` entries. No project references.
- **Dependencies:** Directory.Build.props
- **Test file:** N/A
- **Time:** 2 min

### File #6: `src/EaaS.Infrastructure/EaaS.Infrastructure.csproj`
- **Story:** US-0.1
- **What it does:** Infrastructure project file with all data/messaging dependencies.
- **Key patterns:** References `EaaS.Domain` and `EaaS.Shared`. NuGet packages: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `MassTransit`, `MassTransit.RabbitMQ`, `StackExchange.Redis`, `AWSSDK.SimpleEmailV2`, `Fluid.Core`, `DnsClient`.
- **Dependencies:** EaaS.Domain.csproj, EaaS.Shared.csproj
- **Test file:** N/A
- **Time:** 3 min

### File #7: `src/EaaS.Api/EaaS.Api.csproj`
- **Story:** US-0.1
- **What it does:** API project file. Web SDK. References Domain, Infrastructure, Shared.
- **Key patterns:** `<Project Sdk="Microsoft.NET.Sdk.Web">`. NuGet packages: `MediatR`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Formatting.Compact`, `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Process`, `AspNetCore.HealthChecks.NpgSql`, `AspNetCore.HealthChecks.Redis`, `AspNetCore.HealthChecks.Rabbitmq`, `Swashbuckle.AspNetCore`.
- **Dependencies:** EaaS.Domain.csproj, EaaS.Infrastructure.csproj, EaaS.Shared.csproj
- **Test file:** N/A
- **Time:** 3 min

### File #8: `src/EaaS.Worker/EaaS.Worker.csproj`
- **Story:** US-0.1
- **What it does:** Worker service project file.
- **Key patterns:** `<Project Sdk="Microsoft.NET.Sdk.Worker">`. NuGet: `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Formatting.Compact`. References Domain, Infrastructure, Shared.
- **Dependencies:** EaaS.Domain.csproj, EaaS.Infrastructure.csproj, EaaS.Shared.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #9: `src/EaaS.WebhookProcessor/EaaS.WebhookProcessor.csproj`
- **Story:** US-0.1
- **What it does:** Webhook processor project file. Minimal API.
- **Key patterns:** `<Project Sdk="Microsoft.NET.Sdk.Web">`. NuGet: `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Formatting.Compact`. References Domain, Infrastructure, Shared.
- **Dependencies:** EaaS.Domain.csproj, EaaS.Infrastructure.csproj, EaaS.Shared.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #10: `dashboard/package.json`
- **Story:** US-0.1
- **What it does:** Dashboard project file. Next.js 15. Stub only for Sprint 1.
- **Key patterns:** Next.js 15, shadcn/ui, Tailwind CSS, TanStack Query, Recharts. Standalone Node.js project outside .NET solution.
- **Dependencies:** None (standalone Next.js project)
- **Test file:** N/A
- **Time:** 2 min

### File #11: `tests/EaaS.Api.Tests/EaaS.Api.Tests.csproj`
- **Story:** US-0.1
- **What it does:** API unit test project.
- **Key patterns:** NuGet: `xunit`, `xunit.runner.visualstudio`, `NSubstitute` (replacing Moq), `FluentAssertions`, `coverlet.collector`. References `EaaS.Api`.
- **Dependencies:** EaaS.Api.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #12: `tests/EaaS.Worker.Tests/EaaS.Worker.Tests.csproj`
- **Story:** US-0.1
- **What it does:** Worker unit test project.
- **Key patterns:** Same test NuGet stack. References `EaaS.Worker`.
- **Dependencies:** EaaS.Worker.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #13: `tests/EaaS.Infrastructure.Tests/EaaS.Infrastructure.Tests.csproj`
- **Story:** US-0.1
- **What it does:** Infrastructure integration test project.
- **Key patterns:** Same test NuGet stack + `Testcontainers.PostgreSql`, `Testcontainers.Redis`, `Testcontainers.RabbitMq`. References `EaaS.Infrastructure`.
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #14: `tests/EaaS.Integration.Tests/EaaS.Integration.Tests.csproj`
- **Story:** US-0.1
- **What it does:** End-to-end integration test project.
- **Key patterns:** Same test NuGet stack + all Testcontainers + `Microsoft.AspNetCore.Mvc.Testing` + `Bogus`. References `EaaS.Api`.
- **Dependencies:** EaaS.Api.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #15: `src/EaaS.Api/Program.cs` (stub)
- **Story:** US-0.1
- **What it does:** Minimal API entry point. Initial stub with `builder.Build()` and `app.Run()`. Will be expanded in later phases.
- **Key patterns:** `WebApplication.CreateBuilder(args)`. Minimal -- just enough to compile and start. Serilog bootstrap logging.
- **Dependencies:** EaaS.Api.csproj
- **Test file:** N/A
- **Time:** 5 min

### File #16: `src/EaaS.Worker/Program.cs` (stub)
- **Story:** US-0.1
- **What it does:** Worker service entry point stub.
- **Key patterns:** `Host.CreateDefaultBuilder(args).ConfigureServices(...)`. Serilog bootstrap. Will be expanded when consumers are added.
- **Dependencies:** EaaS.Worker.csproj
- **Test file:** N/A
- **Time:** 5 min

### File #17: `src/EaaS.WebhookProcessor/Program.cs` (stub)
- **Story:** US-0.1
- **What it does:** Webhook processor Minimal API stub.
- **Key patterns:** `WebApplication.CreateBuilder(args)`. Single health endpoint. Full implementation is Sprint 2+.
- **Dependencies:** EaaS.WebhookProcessor.csproj
- **Test file:** N/A
- **Time:** 5 min

### File #18: `dashboard/src/app/page.tsx` (stub)
- **Story:** US-0.1
- **What it does:** Next.js dashboard stub. Returns a basic placeholder page. Sprint 3+ feature.
- **Key patterns:** React Server Component. Minimal -- just renders a placeholder page.
- **Dependencies:** dashboard/package.json
- **Test file:** N/A
- **Time:** 5 min

### File #19: `src/EaaS.Api/Dockerfile`
- **Story:** US-0.1
- **What it does:** Multi-stage Docker build for the API.
- **Key patterns:**
  - Stage 1 (`build`): `mcr.microsoft.com/dotnet/sdk:8.0-alpine`. Copy .csproj files, restore, copy all source, publish.
  - Stage 2 (`runtime`): `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`. Copy published output. Expose 8080. `ENTRYPOINT ["dotnet", "EaaS.Api.dll"]`.
  - Build context is the solution root (`.` in docker-compose), so COPY paths are relative to root.
  - No curl needed; healthcheck uses wget (Alpine includes it).
- **Dependencies:** EaaS.Api.csproj, all referenced .csproj files
- **Test file:** N/A
- **Time:** 10 min

### File #20: `src/EaaS.Worker/Dockerfile`
- **Story:** US-0.1
- **What it does:** Multi-stage Docker build for the Worker.
- **Key patterns:** Same pattern as API Dockerfile but uses `mcr.microsoft.com/dotnet/runtime:8.0-alpine` for runtime (no ASP.NET needed for Worker unless using generic host with web -- actually Worker uses `Host.CreateDefaultBuilder` so no ASP.NET). No exposed port.
- **Dependencies:** EaaS.Worker.csproj
- **Test file:** N/A
- **Time:** 5 min

### File #21: `src/EaaS.WebhookProcessor/Dockerfile`
- **Story:** US-0.1
- **What it does:** Multi-stage Docker build for the WebhookProcessor.
- **Key patterns:** Same pattern as API Dockerfile. `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`. Expose 8081.
- **Dependencies:** EaaS.WebhookProcessor.csproj
- **Test file:** N/A
- **Time:** 5 min

### File #22: `dashboard/Dockerfile`
- **Story:** US-0.1
- **What it does:** Multi-stage Docker build for the Next.js Dashboard.
- **Key patterns:** Stage 1 (`deps`): `node:20-alpine`, install dependencies. Stage 2 (`build`): build Next.js. Stage 3 (`runtime`): `node:20-alpine`, copy standalone output. Expose 3000.
- **Dependencies:** dashboard/package.json
- **Test file:** N/A
- **Time:** 5 min

### File #23: `docker-compose.yml`
- **Story:** US-0.1
- **What it does:** Orchestrates all services: postgres, redis, rabbitmq, api, worker, webhook-processor, dashboard, nginx, certbot.
- **Key patterns:** Exact copy of Architecture section 7 Docker Compose. Resource limits, health checks, `depends_on` with `condition: service_healthy`, localhost-only ports for infrastructure, volumes for persistence.
- **Dependencies:** All Dockerfiles, nginx.conf
- **Test file:** N/A
- **Time:** 10 min

### File #24: `docker-compose.override.yml`
- **Story:** US-0.1
- **What it does:** Local dev overrides: no TLS, no certbot, ports exposed for debugging, seed data volume mounted.
- **Key patterns:**
  - Override nginx to listen on 80 only (no SSL).
  - Mount `scripts/seed-data.sql` into PostgreSQL `docker-entrypoint-initdb.d/02-seed.sql`.
  - Override `ASPNETCORE_ENVIRONMENT=Development` for all .NET services.
  - Expose PostgreSQL on 5432, Redis on 6379, RabbitMQ management on 15672 for local access.
- **Dependencies:** docker-compose.yml
- **Test file:** N/A
- **Time:** 10 min

### File #25: `nginx/nginx.conf`
- **Story:** US-0.1
- **What it does:** Nginx reverse proxy config with TLS, security headers, rate limiting.
- **Key patterns:** Exact copy of Architecture nginx config section 7. Routes: `/api/` -> api:8080, `/health` -> api:8080, `/webhooks/sns` -> webhook-processor:8081, `/track/` -> webhook-processor:8081, `/` -> dashboard:3000. `client_max_body_size 30M`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 5 min

### File #26: `scripts/init-db.sql`
- **Story:** US-0.1
- **What it does:** Initial database setup -- extensions and default tenant insert. EF Core will handle table creation via migrations, but we need `uuid-ossp` and `pgcrypto` extensions and the default tenant.
- **Key patterns:**
  ```sql
  CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
  CREATE EXTENSION IF NOT EXISTS "pgcrypto";
  ```
  The default tenant insert will be handled by EF Core seed data (HasData) rather than raw SQL, to keep migrations as the single schema source of truth.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 5 min

### File #27: `scripts/seed-data.sql`
- **Story:** US-0.1
- **What it does:** Dev-only seed data: test API key, test domain, test template.
- **Key patterns:** Insert a known dev API key with documented plaintext and SHA-256 hash. Only mounted in `docker-compose.override.yml`.
- **Dependencies:** init-db.sql
- **Test file:** N/A
- **Time:** 5 min

### File #28: `.env.example`
- **Story:** US-0.1
- **What it does:** Template for environment variables with descriptions.
- **Key patterns:** All variables from Architecture section 8.1. Comments for each. No actual secrets.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 5 min

### File #29: `.gitignore`
- **Story:** US-0.1
- **What it does:** Standard .NET + Docker gitignore.
- **Key patterns:** Ignore `bin/`, `obj/`, `.env`, `*.user`, `nginx/ssl/`, `appsettings.*.json` (except Development), `.vs/`, test results.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #30: `.editorconfig`
- **Story:** US-0.1
- **What it does:** Code style enforcement.
- **Key patterns:** Standard .NET editorconfig: 4-space indent, UTF-8, LF line endings, `var` usage, namespace style `file_scoped`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

**Phase 1 Verification:** `dotnet build eaas.sln` compiles all projects with zero errors. `docker compose config` validates the compose file. All 4 .NET services start (stubs return 200 on `/health` or minimal response).

---

## 4. Phase 2: Data Layer (US-0.2, US-0.6) -- Target 2h

### US-0.6: Configuration Classes (do first -- everything depends on config)

### File #31: `src/EaaS.Infrastructure/Options/DatabaseOptions.cs`
- **Story:** US-0.6
- **What it does:** Strongly-typed PostgreSQL config options.
- **Key patterns:** Simple POCO with `[Required]` on ConnectionString. Bound via `IOptions<DatabaseOptions>`. Validated at startup via `ValidateDataAnnotations().ValidateOnStart()`.
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A (trivial POCO)
- **Time:** 3 min

### File #32: `src/EaaS.Infrastructure/Options/RedisOptions.cs`
- **Story:** US-0.6
- **What it does:** Strongly-typed Redis config.
- **Key patterns:** ConnectionString with `[Required]`.
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #33: `src/EaaS.Infrastructure/Options/RabbitMqOptions.cs`
- **Story:** US-0.6
- **What it does:** Strongly-typed RabbitMQ config.
- **Key patterns:** Host, Port, Username, Password, VirtualHost, PrefetchCount. All `[Required]` except PrefetchCount (default 10).
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #34: `src/EaaS.Infrastructure/Options/SesOptions.cs`
- **Story:** US-0.6
- **What it does:** Strongly-typed AWS SES config.
- **Key patterns:** Region, AccessKeyId, SecretAccessKey, MaxSendRate. All `[Required]`.
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #35: `src/EaaS.Infrastructure/Options/EmailOptions.cs`
- **Story:** US-0.6
- **What it does:** Email sending limits config.
- **Key patterns:** MaxAttachmentSizeBytes, MaxTotalAttachmentSizeBytes, MaxRecipientsPerEmail, MaxBatchSize, MaxTemplateSizeBytes, LogRetentionDays. Sensible defaults in appsettings.json.
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #36: `src/EaaS.Infrastructure/Options/RateLimitingOptions.cs`
- **Story:** US-0.6
- **What it does:** Rate limiting config.
- **Key patterns:** RequestsPerSecond (default 100), BurstSize (default 20).
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #37: `src/EaaS.Infrastructure/Options/TrackingOptions.cs`
- **Story:** US-0.6
- **What it does:** Tracking pixel/click config.
- **Key patterns:** BaseUrl, PixelPath, ClickPath, HmacSecret.
- **Dependencies:** EaaS.Infrastructure.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #38: `src/EaaS.Api/appsettings.json`
- **Story:** US-0.6
- **What it does:** Base configuration with safe defaults.
- **Key patterns:** Exact copy of Architecture section 8.2. All secrets blank (overridden by environment variables).
- **Dependencies:** Options classes
- **Test file:** N/A
- **Time:** 5 min

### File #39: `src/EaaS.Api/appsettings.Development.json`
- **Story:** US-0.6
- **What it does:** Dev overrides with local connection strings.
- **Key patterns:** PostgreSQL on localhost, Redis on localhost, RabbitMQ guest/guest.
- **Dependencies:** appsettings.json
- **Test file:** N/A
- **Time:** 3 min

### US-0.2: Domain Entities and EF Core

### File #40: `src/EaaS.Domain/Enums/EmailStatus.cs`
- **Story:** US-0.2
- **What it does:** Defines email lifecycle states: Queued, Sending, Sent, Delivered, Bounced, Complained, Failed.
- **Key patterns:** Simple C# enum. Maps to PostgreSQL `email_status` enum type.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 2 min

### File #41: `src/EaaS.Domain/Enums/EventType.cs`
- **Story:** US-0.2
- **What it does:** Email event types: Queued, Sent, Delivered, Bounced, Complained, Opened, Clicked, Failed.
- **Key patterns:** Simple C# enum.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 1 min

### File #42: `src/EaaS.Domain/Enums/DomainStatus.cs`
- **Story:** US-0.2
- **What it does:** Domain verification states: PendingVerification, Verified, Failed, Suspended.
- **Key patterns:** Simple C# enum.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 1 min

### File #43: `src/EaaS.Domain/Enums/ApiKeyStatus.cs`
- **Story:** US-0.2
- **What it does:** API key states: Active, Revoked, Rotating.
- **Key patterns:** Simple C# enum.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 1 min

### File #44: `src/EaaS.Domain/Enums/SuppressionReason.cs`
- **Story:** US-0.2
- **What it does:** Suppression reasons: HardBounce, SoftBounceLimit, Complaint, Manual.
- **Key patterns:** Simple C# enum.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 1 min

### File #45: `src/EaaS.Domain/Enums/DnsRecordType.cs`
- **Story:** US-0.2
- **What it does:** DNS record types: TXT, CNAME, MX.
- **Key patterns:** Simple C# enum.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 1 min

### File #46: `src/EaaS.Domain/Enums/DnsRecordPurpose.cs`
- **Story:** US-0.2
- **What it does:** DNS record purposes: SPF, DKIM, DMARC.
- **Key patterns:** Simple C# enum.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 1 min

### File #47: `src/EaaS.Domain/ValueObjects/EmailAddress.cs`
- **Story:** US-0.2
- **What it does:** Validated email address value object with Address and optional Name.
- **Key patterns:** C# record with private constructor and static `Create(string email, string? name)` factory. Validates RFC 5322 format using `System.Net.Mail.MailAddress`. Throws `ArgumentException` on invalid format. Implements `IEquatable<EmailAddress>`. No external dependencies.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** `tests/EaaS.Api.Tests/ValueObjects/EmailAddressTests.cs`
- **Time:** 5 min

### File #48: `src/EaaS.Domain/ValueObjects/MessageId.cs`
- **Story:** US-0.2
- **What it does:** Strongly-typed message ID: `msg_` prefix + 12 alphanumeric.
- **Key patterns:** C# record wrapping a string Value. Static `New()` generates a random ID. Static `Parse(string)` validates format. Uses `IdGenerator` from Shared.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A (trivial value object)
- **Time:** 3 min

### File #49-52: Remaining value objects
- `src/EaaS.Domain/ValueObjects/BatchId.cs` -- `batch_` prefix + 8 alphanumeric
- `src/EaaS.Domain/ValueObjects/TemplateId.cs` -- `tmpl_` prefix + 8 alphanumeric
- `src/EaaS.Domain/ValueObjects/DomainId.cs` -- `dom_` prefix + 6 alphanumeric
- `src/EaaS.Domain/ValueObjects/KeyId.cs` -- `key_` prefix + 8 alphanumeric
- **Story:** US-0.2
- **Key patterns:** Same pattern as MessageId. Each is a record wrapping a string.
- **Dependencies:** EaaS.Domain.csproj
- **Time:** 8 min total

### File #53: `src/EaaS.Domain/Entities/Tenant.cs`
- **Story:** US-0.2
- **What it does:** Tenant aggregate root. Id, Name, CreatedAt, UpdatedAt.
- **Key patterns:** Plain C# class with Guid Id, string properties. No annotations. No base class. Constructor sets defaults.
- **Dependencies:** EaaS.Domain.csproj
- **Test file:** N/A
- **Time:** 3 min

### File #54: `src/EaaS.Domain/Entities/ApiKey.cs`
- **Story:** US-0.2
- **What it does:** API key entity: Id, TenantId, Name, KeyHash, Prefix, AllowedDomains, Status, CreatedAt, LastUsedAt, RevokedAt.
- **Key patterns:** Plain C# class. `AllowedDomains` is `List<string>`. `Status` is `ApiKeyStatus` enum. `Revoke()` method sets status and timestamp.
- **Dependencies:** Enums/ApiKeyStatus.cs
- **Test file:** N/A
- **Time:** 5 min

### File #55: `src/EaaS.Domain/Entities/Domain.cs`
- **Story:** US-0.2
- **What it does:** Sending domain entity with navigation to DnsRecords.
- **Key patterns:** Plain C# class. `Status` is `DomainStatus`. `DnsRecords` is `List<DnsRecord>`. `Verify()` method updates status and VerifiedAt.
- **Dependencies:** Enums/DomainStatus.cs, DnsRecord entity
- **Test file:** N/A
- **Time:** 5 min

### File #56: `src/EaaS.Domain/Entities/DnsRecord.cs`
- **Story:** US-0.2
- **What it does:** DNS record child entity of Domain.
- **Key patterns:** Id, DomainId, RecordType (string), RecordName, RecordValue, Purpose (DnsRecordPurpose), IsVerified, VerifiedAt, ActualValue, UpdatedAt.
- **Dependencies:** Enums/DnsRecordPurpose.cs
- **Test file:** N/A
- **Time:** 3 min

### File #57: `src/EaaS.Domain/Entities/Template.cs`
- **Story:** US-0.2
- **What it does:** Email template with Liquid bodies. Soft-delete support.
- **Key patterns:** Id, TenantId, Name, SubjectTemplate, HtmlBody, TextBody, VariablesSchema (string/JSON), Version (int), CreatedAt, UpdatedAt, DeletedAt. `Update()` method increments version. `SoftDelete()` sets DeletedAt.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 5 min

### File #58: `src/EaaS.Domain/Entities/Email.cs`
- **Story:** US-0.2
- **What it does:** Email aggregate root with all fields from the DDL.
- **Key patterns:** The most complex entity. Stores From/To/Cc/Bcc as JSON-mapped strings, Subject, HtmlBody, TextBody, TemplateId, Variables, Tags, Metadata, TrackOpens, TrackClicks, Status, SesMessageId, ErrorMessage, all timestamps. Navigation to `EmailEvents`.
- **Dependencies:** Enums/EmailStatus.cs
- **Test file:** N/A
- **Time:** 10 min

### File #59: `src/EaaS.Domain/Entities/EmailEvent.cs`
- **Story:** US-0.2
- **What it does:** Email lifecycle event. Append-only.
- **Key patterns:** Id, EmailId, EventType (enum), Data (string/JSON), CreatedAt.
- **Dependencies:** Enums/EventType.cs
- **Test file:** N/A
- **Time:** 3 min

### File #60: `src/EaaS.Domain/Entities/SuppressionEntry.cs`
- **Story:** US-0.2
- **What it does:** Suppression list entry.
- **Key patterns:** Id, TenantId, EmailAddress, Reason (SuppressionReason), SourceMessageId, SuppressedAt.
- **Dependencies:** Enums/SuppressionReason.cs
- **Test file:** N/A
- **Time:** 3 min

### File #61: `src/EaaS.Domain/Entities/Webhook.cs`
- **Story:** US-0.2
- **What it does:** Webhook endpoint config. Table created in Sprint 1, feature in Sprint 3+.
- **Key patterns:** Id, TenantId, Url, Events (List<string>), Secret, Status, CreatedAt, UpdatedAt.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 3 min

### File #62: `src/EaaS.Domain/Exceptions/DomainException.cs`
- **Story:** US-0.2
- **What it does:** Base domain exception class.
- **Key patterns:** Inherits `Exception`. Has `ErrorCode` string property for mapping to API error codes.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### Files #63-68: Domain exceptions
- `RecipientSuppressedException.cs` -- ErrorCode = "RECIPIENT_SUPPRESSED"
- `DomainNotVerifiedException.cs` -- ErrorCode = "DOMAIN_NOT_VERIFIED"
- `TemplateNotFoundException.cs` -- ErrorCode = "NOT_FOUND"
- `TemplateRenderException.cs` -- ErrorCode = "VALIDATION_ERROR"
- `DuplicateDomainException.cs` -- ErrorCode = "CONFLICT"
- `AttachmentTooLargeException.cs` -- ErrorCode = "VALIDATION_ERROR"
- **Story:** US-0.2
- **Key patterns:** Each extends `DomainException`. Constructor takes a descriptive message.
- **Dependencies:** DomainException.cs
- **Test file:** N/A
- **Time:** 6 min total

### File #69: `src/EaaS.Domain/Interfaces/IEmailRepository.cs`
- **Story:** US-0.2
- **What it does:** Repository interface for Email CRUD.
- **Key patterns:** `Task<Email?> GetByMessageIdAsync(string messageId)`, `Task<Email?> GetByIdAsync(Guid id)`, `Task AddAsync(Email email)`, `Task UpdateAsync(Email email)`, `Task<(List<Email>, int total)> ListAsync(...)` with filtering parameters.
- **Dependencies:** Email entity
- **Test file:** N/A
- **Time:** 5 min

### Files #70-75: Remaining repository interfaces
- `ITemplateRepository.cs` -- CRUD + soft delete + list
- `IDomainRepository.cs` -- CRUD + list + GetByDomainName
- `IApiKeyRepository.cs` -- CRUD + GetByKeyHash + list
- `ISuppressionRepository.cs` -- Add + GetByEmail + list
- `IWebhookRepository.cs` -- CRUD + list by tenant
- **Story:** US-0.2
- **Key patterns:** Async methods. Return nullables for single-item queries. Each returns the relevant entity type.
- **Dependencies:** Respective entity classes
- **Test file:** N/A
- **Time:** 15 min total

### File #76: `src/EaaS.Domain/Interfaces/IUnitOfWork.cs`
- **Story:** US-0.2
- **What it does:** Transaction commit abstraction.
- **Key patterns:** Single method: `Task<int> SaveChangesAsync(CancellationToken ct = default)`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### Files #77-82: Service interfaces
- `IMessagePublisher.cs` -- `Task PublishSendEmailAsync(SendEmailMessage msg, CancellationToken ct)`
- `IEmailDeliveryService.cs` -- `Task<string> SendAsync(Email email, string renderedHtml, string? renderedText, CancellationToken ct)` returns SES message ID
- `ITemplateRenderer.cs` -- `Task<(string html, string? text)> RenderAsync(Template template, Dictionary<string,object> variables)`
- `IDnsVerifier.cs` -- `Task<List<DnsVerificationResult>> VerifyAsync(List<DnsRecord> expectedRecords)`
- `ISuppressionCache.cs` -- `Task<bool> IsSuppressedAsync(string tenantId, string email)`, `Task AddAsync(string tenantId, string email)`
- `IDateTimeProvider.cs` -- `DateTime UtcNow { get; }`
- **Story:** US-0.2
- **Key patterns:** All async except IDateTimeProvider. Return concrete types, not entities where the interface is a service abstraction.
- **Dependencies:** Domain entities
- **Test file:** N/A
- **Time:** 12 min total

### File #83: `src/EaaS.Shared/Models/ApiResponse.cs`
- **Story:** US-0.2
- **What it does:** Generic success response envelope.
- **Key patterns:** `public record ApiResponse<T>(bool Success, T Data)` with static factory `Success(T data)`.
- **Dependencies:** EaaS.Shared.csproj
- **Test file:** N/A
- **Time:** 3 min

### File #84: `src/EaaS.Shared/Models/ApiErrorResponse.cs`
- **Story:** US-0.2
- **What it does:** Error response envelope.
- **Key patterns:** `public record ApiErrorResponse(bool Success, ApiError Error)` with static factory `Error(string code, string message, List<ErrorDetail>? details)`.
- **Dependencies:** ErrorDetail.cs
- **Test file:** N/A
- **Time:** 3 min

### File #85: `src/EaaS.Shared/Models/ErrorDetail.cs`
- **Story:** US-0.2
- **What it does:** Field-level error detail.
- **Key patterns:** `public record ErrorDetail(string Field, string Message)`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 1 min

### File #86: `src/EaaS.Shared/Models/PagedRequest.cs`
- **Story:** US-0.2
- **What it does:** Pagination request parameters.
- **Key patterns:** `public record PagedRequest(int Page = 1, int PageSize = 50, string? SortBy = null, string? SortDir = "desc")`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #87: `src/EaaS.Shared/Models/PagedResponse.cs`
- **Story:** US-0.2
- **What it does:** Paginated response wrapper.
- **Key patterns:** `public record PagedResponse<T>(List<T> Items, int Total, int Page, int PageSize, int TotalPages)`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #88: `src/EaaS.Shared/Constants/ErrorCodes.cs`
- **Story:** US-0.2
- **What it does:** String constants for all error codes.
- **Key patterns:** `public static class ErrorCodes { public const string VALIDATION_ERROR = "VALIDATION_ERROR"; ... }`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 3 min

### File #89: `src/EaaS.Shared/Constants/QueueNames.cs`
- **Story:** US-0.2
- **What it does:** Queue name constants for MassTransit.
- **Key patterns:** `public const string EmailSend = "eaas.emails.send"`. Note: MassTransit auto-generates queue names from consumer class names, so these constants are primarily for reference and any manual publishing.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #90: `src/EaaS.Shared/Constants/CacheKeys.cs`
- **Story:** US-0.2
- **What it does:** Redis key patterns.
- **Key patterns:** `public static string Suppression(string email) => $"suppression:{email}"`. Template cache keys, rate limit keys.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 3 min

### File #91: `src/EaaS.Shared/Helpers/IdGenerator.cs`
- **Story:** US-0.2
- **What it does:** Static methods to generate typed IDs with prefixes.
- **Key patterns:** Uses `RandomNumberGenerator.GetBytes()` for cryptographic randomness. `NewMessageId()` returns `msg_` + 12 alphanumeric. `NewKeyId()`, `NewTemplateId()`, `NewDomainId()`, `NewBatchId()`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 5 min

### File #92: `src/EaaS.Shared/Extensions/StringExtensions.cs`
- **Story:** US-0.2
- **What it does:** `ToSha256Hash()` extension method on string.
- **Key patterns:** `SHA256.HashData(Encoding.UTF8.GetBytes(input))` -> hex string.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 3 min

### File #93: `src/EaaS.Shared/Extensions/DateTimeExtensions.cs`
- **Story:** US-0.2
- **What it does:** `ToUnixTimestamp()` and date range helpers.
- **Key patterns:** `DateTimeOffset.ToUnixTimeSeconds()`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #94: `src/EaaS.Infrastructure/Persistence/EaaSDbContext.cs`
- **Story:** US-0.2
- **What it does:** EF Core DbContext with all DbSets and model configuration.
- **Key patterns:**
  - `DbSet<T>` for every entity: Tenants, ApiKeys, Domains, DnsRecords, Templates, Emails, EmailEvents, SuppressionEntries, Webhooks, DashboardUsers.
  - `OnModelCreating`: applies all `IEntityTypeConfiguration` via `modelBuilder.ApplyConfigurationsFromAssembly(typeof(EaaSDbContext).Assembly)`.
  - PostgreSQL enum mapping: `modelBuilder.HasPostgresEnum<EmailStatus>()`, etc. for all 6 enums.
  - `UseSnakeCaseNamingConvention()` from Npgsql.
  - Seed data: Default tenant with fixed GUID.
  - `SaveChangesAsync` override: auto-set `UpdatedAt` on modified entities via change tracker.
- **Dependencies:** All entities, all enum types, Npgsql EF Core
- **Test file:** `tests/EaaS.Infrastructure.Tests/Persistence/EaaSDbContextTests.cs`
- **Time:** 20 min

### Files #95-103: EF Core entity configurations
Each configuration class implements `IEntityTypeConfiguration<T>` and specifies:
- Table name (snake_case)
- Primary key
- Property types, lengths, required/optional
- Indexes (matching DDL exactly)
- Relationships and foreign keys
- Value conversions for enums
- JSON column mappings (for JSONB fields like `to_emails`, `variables`, `metadata`)
- Unique constraints

| # | File | Entity | Time |
|---|------|--------|------|
| 95 | `Infrastructure/Persistence/Configurations/TenantConfiguration.cs` | Tenant | 3 min |
| 96 | `Infrastructure/Persistence/Configurations/ApiKeyConfiguration.cs` | ApiKey | 5 min |
| 97 | `Infrastructure/Persistence/Configurations/DomainConfiguration.cs` | Domain (the entity) | 5 min |
| 98 | `Infrastructure/Persistence/Configurations/DnsRecordConfiguration.cs` | DnsRecord | 4 min |
| 99 | `Infrastructure/Persistence/Configurations/TemplateConfiguration.cs` | Template | 5 min |
| 100 | `Infrastructure/Persistence/Configurations/EmailConfiguration.cs` | Email | 8 min |
| 101 | `Infrastructure/Persistence/Configurations/EmailEventConfiguration.cs` | EmailEvent | 3 min |
| 102 | `Infrastructure/Persistence/Configurations/SuppressionEntryConfiguration.cs` | SuppressionEntry | 4 min |
| 103 | `Infrastructure/Persistence/Configurations/WebhookConfiguration.cs` | Webhook | 3 min |

- **Story:** US-0.2
- **Dependencies:** EaaSDbContext.cs, respective entities
- **Test file:** Covered by EaaSDbContext integration test

### File #104: `src/EaaS.Infrastructure/Persistence/UnitOfWork.cs`
- **Story:** US-0.2
- **What it does:** Implements `IUnitOfWork`. Wraps `DbContext.SaveChangesAsync`.
- **Key patterns:** Constructor-injected `EaaSDbContext`. Single method delegates to `_context.SaveChangesAsync(ct)`.
- **Dependencies:** EaaSDbContext.cs, IUnitOfWork.cs
- **Test file:** N/A (trivial wrapper)
- **Time:** 3 min

### File #105: Generate initial EF Core migration
- **Story:** US-0.2
- **What it does:** Creates the initial migration that builds all tables.
- **Key patterns:** `dotnet ef migrations add InitialCreate --project src/EaaS.Infrastructure --startup-project src/EaaS.Api`. Verify the generated migration matches the DDL from Architecture section 3.
- **Dependencies:** EaaSDbContext.cs, all configurations
- **Test file:** N/A
- **Time:** 10 min

**Phase 2 Verification:** `dotnet ef database update` creates all tables. Manual SQL query confirms table structure matches DDL. Options classes bind correctly from appsettings.json.

---

## 5. Phase 3: Infrastructure (US-0.3, US-0.4, US-0.5) -- Target 2h

### US-0.3: Redis and RabbitMQ Configuration

### File #106: `src/EaaS.Infrastructure/Caching/RedisSuppressionCache.cs`
- **Story:** US-0.3
- **What it does:** Implements `ISuppressionCache`. SET/GET suppressed emails in Redis.
- **Key patterns:** Injects `IConnectionMultiplexer`. `IsSuppressedAsync`: `db.KeyExistsAsync(CacheKeys.Suppression(email))`. `AddAsync`: `db.StringSetAsync(key, "1", expiry: null)` (permanent until explicitly removed).
- **Dependencies:** ISuppressionCache interface, CacheKeys constants, StackExchange.Redis
- **Test file:** `tests/EaaS.Infrastructure.Tests/Caching/RedisSuppressionCacheTests.cs`
- **Time:** 10 min

### File #107: `src/EaaS.Infrastructure/Caching/RedisTemplateCache.cs`
- **Story:** US-0.3
- **What it does:** Caches rendered templates in Redis with 15-minute TTL.
- **Key patterns:** `GetAsync(string templateId)` returns cached template body or null. `SetAsync(string templateId, string html, string? text)` stores with TTL. `InvalidateAsync(string templateId)` deletes the key.
- **Dependencies:** StackExchange.Redis, CacheKeys
- **Test file:** N/A (tested via integration)
- **Time:** 8 min

### File #108: `src/EaaS.Infrastructure/Caching/RedisRateLimiter.cs`
- **Story:** US-0.3
- **What it does:** Sliding window rate limiter using Redis sorted sets.
- **Key patterns:** Lua script for atomicity:
  1. ZREMRANGEBYSCORE to remove entries older than window
  2. ZCARD to count
  3. If under limit: ZADD + EXPIRE
  4. Return count
  Injects `IConnectionMultiplexer` and `IOptions<RateLimitingOptions>`.
- **Dependencies:** StackExchange.Redis, RateLimitingOptions
- **Test file:** `tests/EaaS.Infrastructure.Tests/Caching/RedisRateLimiterTests.cs`
- **Time:** 15 min

### File #109: `src/EaaS.Infrastructure/Messaging/Messages/SendEmailMessage.cs`
- **Story:** US-0.3
- **What it does:** Message contract for the email send queue.
- **Key patterns:** C# record with all fields from Architecture section 5.3. EmailId, MessageId, TenantId, From, To, Cc, Bcc, Subject, HtmlBody, TextBody, TemplateId, Variables, TrackOpens, TrackClicks, EnqueuedAt.
- **Dependencies:** None (plain record)
- **Test file:** N/A
- **Time:** 5 min

### File #110: `src/EaaS.Infrastructure/Messaging/Messages/ProcessWebhookMessage.cs`
- **Story:** US-0.3
- **What it does:** Message contract for webhook delivery queue.
- **Key patterns:** WebhookId, Url, Secret, Event, Payload.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 3 min

### File #111: `src/EaaS.Infrastructure/Messaging/MassTransitPublisher.cs`
- **Story:** US-0.3
- **What it does:** Implements `IMessagePublisher`. Publishes messages via MassTransit `IBus`.
- **Key patterns:** Injects `IBus`. `PublishSendEmailAsync(msg, ct)` calls `_bus.Publish(msg, ct)`. Sets correlation ID in message header.
- **Dependencies:** IMessagePublisher, IBus (MassTransit)
- **Test file:** `tests/EaaS.Infrastructure.Tests/Messaging/MassTransitPublisherTests.cs`
- **Time:** 8 min

### File #112: `src/EaaS.Infrastructure/Messaging/MassTransitConfiguration.cs`
- **Story:** US-0.3
- **What it does:** MassTransit + RabbitMQ bus configuration as an `IServiceCollection` extension method.
- **Key patterns:**
  ```csharp
  services.AddMassTransit(x =>
  {
      x.AddConsumer<SendEmailConsumer>();
      x.UsingRabbitMq((context, cfg) =>
      {
          cfg.Host(options.Host, options.VirtualHost, h => { ... });
          cfg.UseDelayedRedelivery(r => r.Intervals(1m, 5m, 30m));
          cfg.UseMessageRetry(r => r.Immediate(1));
          cfg.PrefetchCount = options.PrefetchCount;
          cfg.ConfigureEndpoints(context);
      });
  });
  ```
  Called from `DependencyInjection.AddInfrastructure()`.
- **Dependencies:** RabbitMqOptions, MassTransit.RabbitMQ, Consumer classes (forward reference -- consumers added in Phase 5)
- **Test file:** N/A (integration tested)
- **Time:** 15 min

### US-0.4: Health Check Endpoint

### File #113: `src/EaaS.Infrastructure/Email/SesHealthCheck.cs`
- **Story:** US-0.4
- **What it does:** `IHealthCheck` that calls `GetSendQuota` to verify SES connectivity.
- **Key patterns:** Returns Healthy with remaining quota in data, or Unhealthy with exception. Timeout: 5 seconds.
- **Dependencies:** AWSSDK.SimpleEmailV2, SesOptions
- **Test file:** N/A (requires AWS -- verified via integration)
- **Time:** 8 min

### File #114: `src/EaaS.Infrastructure/HealthChecks/RabbitMqHealthCheck.cs`
- **Story:** US-0.4
- **What it does:** `IHealthCheck` for RabbitMQ. Uses the `AspNetCore.HealthChecks.Rabbitmq` NuGet package.
- **Key patterns:** Actually, use the NuGet health check package directly in DI registration: `builder.Services.AddHealthChecks().AddRabbitMQ()`. This file is not needed if using the package. Instead, configure in `Program.cs`.
- **Decision:** Use NuGet health check packages (`AspNetCore.HealthChecks.NpgSql`, `.Redis`, `.Rabbitmq`) instead of custom health checks for PostgreSQL, Redis, and RabbitMQ. Only SES needs a custom health check.
- **Dependencies:** Health check NuGet packages
- **Test file:** N/A
- **Time:** 5 min (configuration in Program.cs)

### File #115: `src/EaaS.Api/Features/Health/HealthEndpoint.cs`
- **Story:** US-0.4
- **What it does:** Maps `GET /health` endpoint. Returns custom JSON format matching Architecture spec (not the default ASP.NET health check format).
- **Key patterns:** `app.MapGet("/health", ...)`. Runs `HealthCheckService.CheckHealthAsync()`. Transforms the built-in `HealthReport` into the custom response format with `status`, `version`, `uptime_seconds`, `components` map. Returns 200 if healthy, 503 if any component is unhealthy. No authentication required.
- **Dependencies:** Health check registrations, SesHealthCheck
- **Test file:** `tests/EaaS.Api.Tests/Features/Health/HealthEndpointTests.cs`
- **Time:** 15 min

### US-0.5: Structured Logging

### File #116: Update `src/EaaS.Api/Program.cs` (Serilog setup)
- **Story:** US-0.5
- **What it does:** Configure Serilog as the logging provider.
- **Key patterns:**
  ```csharp
  Log.Logger = new LoggerConfiguration()
      .ReadFrom.Configuration(builder.Configuration)
      .CreateLogger();
  builder.Host.UseSerilog();
  ```
  Reads Serilog config from appsettings.json. Bootstrap logger for startup errors.
- **Dependencies:** appsettings.json Serilog section, Serilog NuGet packages
- **Test file:** N/A
- **Time:** 5 min

### File #117: `src/EaaS.Api/Middleware/RequestLoggingMiddleware.cs`
- **Story:** US-0.5
- **What it does:** Logs request method, path, status code, and duration. Generates or reads correlation ID.
- **Key patterns:** `IMiddleware` implementation. Reads `X-Request-Id` header or generates GUID. Pushes `CorrelationId` to `LogContext`. Wraps `_next(context)` with `Stopwatch`. Logs at Information level: `"HTTP {Method} {Path} responded {StatusCode} in {Duration}ms"`.
- **Dependencies:** Serilog
- **Test file:** `tests/EaaS.Api.Tests/Middleware/RequestLoggingMiddlewareTests.cs`
- **Time:** 10 min

### File #118: `src/EaaS.Api/Middleware/GlobalExceptionHandler.cs`
- **Story:** US-0.5 (logging) + US-0.2 (error format)
- **What it does:** Catches all unhandled exceptions and maps to API error responses.
- **Key patterns:** Implements `IExceptionHandler` (.NET 8). Switch on exception type:
  - `ValidationException` -> 400
  - Domain exceptions -> mapped per ErrorCode
  - `KeyNotFoundException` -> 404
  - Everything else -> 500
  Logs exception at Error level. Returns `ApiErrorResponse` JSON.
- **Dependencies:** Domain exceptions, ApiErrorResponse, Serilog
- **Test file:** `tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs`
- **Time:** 15 min

### File #119: `src/EaaS.Infrastructure/DependencyInjection.cs`
- **Story:** US-0.3 + US-0.5 + US-0.6
- **What it does:** `AddInfrastructure(IConfiguration)` extension method that registers everything.
- **Key patterns:**
  - Options: `services.AddOptions<DatabaseOptions>().BindConfiguration("ConnectionStrings").ValidateDataAnnotations().ValidateOnStart()`. Same for Redis, RabbitMQ, SES, Email, RateLimiting, Tracking.
  - EF Core: `services.AddDbContext<EaaSDbContext>(o => o.UseNpgsql(connStr).UseSnakeCaseNamingConvention())`.
  - Redis: `services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(...))`.
  - MassTransit: calls `MassTransitConfiguration.AddMassTransit(services, config)`.
  - Repositories: `services.AddScoped<IEmailRepository, EmailRepository>()`. Same for all repositories.
  - Services: `services.AddScoped<IUnitOfWork, UnitOfWork>()`, etc.
  - Health checks: `services.AddHealthChecks().AddNpgSql().AddRedis().AddRabbitMQ().AddCheck<SesHealthCheck>("ses")`.
- **Dependencies:** All Options classes, all repository implementations, all service implementations
- **Test file:** N/A
- **Time:** 20 min

### File #120: `src/EaaS.Api/DependencyInjection.cs`
- **Story:** US-0.3
- **What it does:** `AddApiServices()` extension method for API-specific services.
- **Key patterns:**
  - MediatR: `services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>())`.
  - FluentValidation: `services.AddValidatorsFromAssemblyContaining<Program>()`.
  - Validation behavior: `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))`.
  - Authentication: `services.AddAuthentication("ApiKey").AddScheme<...>("ApiKey", ...)`.
  - Exception handler: `services.AddExceptionHandler<GlobalExceptionHandler>()`.
  - Middleware registrations.
- **Dependencies:** All middleware, validation behavior
- **Test file:** N/A
- **Time:** 10 min

### File #121: `src/EaaS.Api/Behaviors/ValidationBehavior.cs`
- **Story:** US-0.3
- **What it does:** MediatR pipeline behavior that runs FluentValidation before the handler.
- **Key patterns:**
  ```csharp
  public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
  {
      private readonly IEnumerable<IValidator<TRequest>> _validators;
      // Runs all validators, aggregates errors, throws ValidationException if any
  }
  ```
- **Dependencies:** FluentValidation, MediatR
- **Test file:** `tests/EaaS.Api.Tests/Behaviors/ValidationBehaviorTests.cs`
- **Time:** 10 min

### File #122: `src/EaaS.Infrastructure/Services/UtcDateTimeProvider.cs`
- **Story:** US-0.3
- **What it does:** `IDateTimeProvider` implementation returning `DateTime.UtcNow`.
- **Key patterns:** Trivial. Exists for testability.
- **Dependencies:** IDateTimeProvider
- **Test file:** N/A
- **Time:** 2 min

**Phase 3 Verification:** Health endpoint returns 200 with all components healthy. Serilog outputs JSON to console. Redis and RabbitMQ connections established. MassTransit bus starts without errors.

---

## 6. Phase 4: Core Features (US-5.1, US-5.3, US-3.1, US-3.2) -- Target 4h

### Repositories (needed by all features)

### Files #123-128: Repository implementations

| # | File | Interface | Key Details | Time |
|---|------|-----------|-------------|------|
| 123 | `Infrastructure/Persistence/Repositories/ApiKeyRepository.cs` | IApiKeyRepository | `GetByKeyHashAsync` queries by hash + active status. Includes `SendCount` via subquery on emails table. | 12 min |
| 124 | `Infrastructure/Persistence/Repositories/DomainRepository.cs` | IDomainRepository | `GetByDomainNameAsync(tenantId, name)`. Includes `DnsRecords` navigation. | 10 min |
| 125 | `Infrastructure/Persistence/Repositories/TemplateRepository.cs` | ITemplateRepository | Filters out soft-deleted (DeletedAt != null). `GetByNameAsync(tenantId, name)` for uniqueness. | 10 min |
| 126 | `Infrastructure/Persistence/Repositories/EmailRepository.cs` | IEmailRepository | `GetByMessageIdAsync`. `ListAsync` with filtering, sorting, pagination. Complex LINQ. | 15 min |
| 127 | `Infrastructure/Persistence/Repositories/SuppressionRepository.cs` | ISuppressionRepository | `GetByEmailAsync(tenantId, email)`. Simple add/check. | 8 min |
| 128 | `Infrastructure/Persistence/Repositories/WebhookRepository.cs` | IWebhookRepository | Basic CRUD. Minimal for Sprint 1 (table exists, feature is Sprint 3+). | 5 min |

- **Story:** US-0.2 (data access for all features)
- **Dependencies:** EaaSDbContext, entity classes, interfaces
- **Test files:** `tests/EaaS.Infrastructure.Tests/Persistence/{Name}RepositoryTests.cs`

### US-5.1: Create API Key

### File #129: `src/EaaS.Api/Middleware/ApiKeyAuthenticationHandler.cs`
- **Story:** US-5.1
- **What it does:** Custom `AuthenticationHandler` that validates Bearer tokens against hashed API keys.
- **Key patterns:**
  - Extends `AuthenticationHandler<AuthenticationSchemeOptions>`.
  - `HandleAuthenticateAsync()`: extract Bearer token -> SHA-256 hash -> lookup in DB (via `IApiKeyRepository.GetByKeyHashAsync`) -> if found and active, create `ClaimsPrincipal` with tenant_id and api_key_id claims -> update `LastUsedAt` fire-and-forget.
  - Redis cache: check `apikey:{hash}` in Redis first, cache for 5 minutes on hit.
  - Returns `AuthenticateResult.Fail("Invalid API key")` on failure.
- **Dependencies:** IApiKeyRepository, IConnectionMultiplexer, StringExtensions.ToSha256Hash()
- **Test file:** `tests/EaaS.Api.Tests/Middleware/ApiKeyAuthenticationHandlerTests.cs`
- **Time:** 25 min

### File #130: `src/EaaS.Api/Middleware/RateLimitingMiddleware.cs`
- **Story:** US-5.1
- **What it does:** Per-API-key rate limiting middleware.
- **Key patterns:** `IMiddleware`. Extracts `api_key_id` from claims. Calls `RedisRateLimiter.CheckAsync(apiKeyId)`. If over limit: returns 429 with `Retry-After` header and `ApiErrorResponse` with code `RATE_LIMITED`. Otherwise: allows request to proceed.
- **Dependencies:** RedisRateLimiter, Claims
- **Test file:** `tests/EaaS.Api.Tests/Middleware/RateLimitingMiddlewareTests.cs`
- **Time:** 10 min

### File #131: `src/EaaS.Api/Features/ApiKeys/CreateApiKey/CreateApiKeyRequest.cs`
- **Story:** US-5.1
- **What it does:** Request DTO: `Name`, `AllowedDomains`.
- **Key patterns:** C# record. JSON property names: `name`, `allowed_domains`.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #132: `src/EaaS.Api/Features/ApiKeys/CreateApiKey/CreateApiKeyResponse.cs`
- **Story:** US-5.1
- **What it does:** Response DTO: KeyId, Name, ApiKey (plaintext, shown once), Prefix, AllowedDomains, CreatedAt.
- **Key patterns:** C# record.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #133: `src/EaaS.Api/Features/ApiKeys/CreateApiKey/CreateApiKeyValidator.cs`
- **Story:** US-5.1
- **What it does:** Validates: Name required, max 255 chars. AllowedDomains each must be valid domain format.
- **Key patterns:** `AbstractValidator<CreateApiKeyCommand>`. `RuleFor(x => x.Name).NotEmpty().MaximumLength(255)`.
- **Dependencies:** FluentValidation
- **Test file:** `tests/EaaS.Api.Tests/Features/ApiKeys/CreateApiKeyValidatorTests.cs`
- **Time:** 5 min

### File #134: `src/EaaS.Api/Features/ApiKeys/CreateApiKey/CreateApiKeyCommand.cs`
- **Story:** US-5.1
- **What it does:** MediatR command: `IRequest<CreateApiKeyResponse>`.
- **Key patterns:** C# record with same fields as request, plus TenantId from claims.
- **Dependencies:** MediatR
- **Test file:** N/A
- **Time:** 2 min

### File #135: `src/EaaS.Api/Features/ApiKeys/CreateApiKey/CreateApiKeyCommandHandler.cs`
- **Story:** US-5.1
- **What it does:** Generates API key, hashes, stores, returns.
- **Key patterns:**
  1. Generate random key: `eaas_live_` + 40 alphanumeric chars
  2. Compute SHA-256 hash
  3. Extract prefix (first 8 chars)
  4. Create `ApiKey` entity with hash, prefix, name, tenant_id, allowed_domains
  5. Save via `IApiKeyRepository.AddAsync` + `IUnitOfWork.SaveChangesAsync`
  6. Return response with plaintext key (only time it is returned)
- **Dependencies:** IApiKeyRepository, IUnitOfWork, IdGenerator, StringExtensions
- **Test file:** `tests/EaaS.Api.Tests/Features/ApiKeys/CreateApiKeyCommandHandlerTests.cs`
- **Time:** 15 min

### File #136: `src/EaaS.Api/Features/ApiKeys/CreateApiKey/CreateApiKeyEndpoint.cs`
- **Story:** US-5.1
- **What it does:** Maps `POST /api/v1/keys` to MediatR command.
- **Key patterns:** `app.MapPost("/api/v1/keys", ...)`. Extracts TenantId from claims. Creates command from request DTO. Sends via MediatR. Returns `Results.Created($"/api/v1/keys/{response.KeyId}", ApiResponse.Success(response))`.
- **Dependencies:** MediatR, CreateApiKeyCommand
- **Test file:** Covered by integration tests
- **Time:** 5 min

### API Key Bootstrap (CLI seed command)

### File #137: Seed command logic in `src/EaaS.Api/Program.cs`
- **Story:** US-5.1
- **What it does:** Handles `dotnet run -- seed --api-key` command-line argument.
- **Key patterns:** Before `app.Run()`, check `args` for `seed` command. If present:
  1. Build service provider manually
  2. Resolve `EaaSDbContext`
  3. Generate key, hash, insert into api_keys table directly
  4. Print plaintext key to console
  5. Exit with code 0
  Also handles `seed --dashboard-password` for bcrypt hash generation.
- **Dependencies:** EaaSDbContext, SHA-256
- **Test file:** N/A (manual verification)
- **Time:** 15 min

### US-5.3: Revoke API Key

### File #138: `src/EaaS.Api/Features/ApiKeys/RevokeApiKey/RevokeApiKeyCommand.cs`
- **Story:** US-5.3
- **What it does:** MediatR command: KeyId + TenantId.
- **Dependencies:** MediatR
- **Test file:** N/A
- **Time:** 2 min

### File #139: `src/EaaS.Api/Features/ApiKeys/RevokeApiKey/RevokeApiKeyCommandHandler.cs`
- **Story:** US-5.3
- **What it does:** Sets status to Revoked, sets RevokedAt, invalidates Redis cache.
- **Key patterns:** Fetch key by ID and tenant. If not found: throw `KeyNotFoundException`. Call `apiKey.Revoke()`. Save. Delete Redis cache entry `apikey:{hash}`.
- **Dependencies:** IApiKeyRepository, IUnitOfWork, IConnectionMultiplexer
- **Test file:** `tests/EaaS.Api.Tests/Features/ApiKeys/RevokeApiKeyCommandHandlerTests.cs`
- **Time:** 10 min

### File #140: `src/EaaS.Api/Features/ApiKeys/RevokeApiKey/RevokeApiKeyEndpoint.cs`
- **Story:** US-5.3
- **What it does:** Maps `DELETE /api/v1/keys/{id}`. Returns 204 No Content.
- **Dependencies:** MediatR
- **Test file:** Covered by integration tests
- **Time:** 5 min

### US-3.1: Add Sending Domain

### File #141: `src/EaaS.Infrastructure/Dns/SesDomainIdentityService.cs`
- **Story:** US-3.1
- **What it does:** Calls SES `VerifyDomainIdentity` and `VerifyDomainDkim`. Returns verification token and DKIM tokens.
- **Key patterns:** Injects `IAmazonSimpleEmailServiceV2` (or v1 for these specific calls -- VerifyDomainIdentity is SES v1 only). Actually, `VerifyDomainIdentity` and `VerifyDomainDkim` are in the SES v1 SDK. We need to add `AWSSDK.SimpleEmail` (v1) NuGet package for these calls, OR use the v2 `CreateEmailIdentity` API which handles both domain verification and DKIM in one call.

  **Decision:** Use SES v2 `CreateEmailIdentity` which initiates DKIM signing and domain verification in a single API call. Returns DKIM tokens in the response. This avoids needing the v1 SDK.

  ```csharp
  var response = await _sesClient.CreateEmailIdentityAsync(new CreateEmailIdentityRequest
  {
      EmailIdentity = domainName,
      DkimSigningAttributes = new DkimSigningAttributes
      {
          NextSigningKeyLength = DkimSigningKeyLength.RSA_2048_BIT
      }
  });
  ```
  Returns `DkimAttributes.Tokens` (3 DKIM tokens).
- **Dependencies:** AWSSDK.SimpleEmailV2, SesOptions
- **Test file:** N/A (mocked in handler tests)
- **Time:** 15 min

### File #142: `src/EaaS.Api/Features/Domains/AddDomain/AddDomainRequest.cs`
- **Story:** US-3.1
- **What it does:** Request DTO: `Domain` (string).
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #143: `src/EaaS.Api/Features/Domains/AddDomain/AddDomainResponse.cs`
- **Story:** US-3.1
- **What it does:** Response DTO: DomainId, Domain, Status, DnsRecords[], CreatedAt.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 3 min

### File #144: `src/EaaS.Api/Features/Domains/AddDomain/AddDomainValidator.cs`
- **Story:** US-3.1
- **What it does:** Validates domain format (RFC 1035).
- **Key patterns:** Regex for valid domain. `RuleFor(x => x.Domain).NotEmpty().Matches(domainRegex)`.
- **Dependencies:** FluentValidation
- **Test file:** `tests/EaaS.Api.Tests/Features/Domains/AddDomainValidatorTests.cs`
- **Time:** 5 min

### File #145: `src/EaaS.Api/Features/Domains/AddDomain/AddDomainCommand.cs`
- **Story:** US-3.1
- **What it does:** MediatR command.
- **Dependencies:** MediatR
- **Test file:** N/A
- **Time:** 2 min

### File #146: `src/EaaS.Api/Features/Domains/AddDomain/AddDomainCommandHandler.cs`
- **Story:** US-3.1
- **What it does:** Calls SES to initiate domain verification, generates DNS records, saves to DB.
- **Key patterns:**
  1. Check for duplicate domain via `IDomainRepository.GetByDomainNameAsync`. If exists: throw `DuplicateDomainException`.
  2. Call `SesDomainIdentityService.CreateIdentityAsync(domainName)` -> get DKIM tokens.
  3. Create `Domain` entity with status `PendingVerification`.
  4. Create 5 `DnsRecord` entities: 1 SPF TXT, 3 DKIM CNAME, 1 DMARC TXT. Generate values based on DKIM tokens and domain name (exact format per Architecture section 6.2).
  5. Save via repository + UoW.
  6. Return response with DNS records.
- **Dependencies:** IDomainRepository, IUnitOfWork, SesDomainIdentityService, IDateTimeProvider
- **Test file:** `tests/EaaS.Api.Tests/Features/Domains/AddDomainCommandHandlerTests.cs`
- **Time:** 20 min

### File #147: `src/EaaS.Api/Features/Domains/AddDomain/AddDomainEndpoint.cs`
- **Story:** US-3.1
- **What it does:** Maps `POST /api/v1/domains`. Returns 201.
- **Dependencies:** MediatR
- **Test file:** Covered by integration tests
- **Time:** 5 min

### US-3.2: Verify Domain DNS

### File #148: `src/EaaS.Infrastructure/Dns/DnsVerifier.cs`
- **Story:** US-3.2
- **What it does:** Implements `IDnsVerifier`. Uses DnsClient.NET to query DNS records.
- **Key patterns:** Injects `ILookupClient` (from DnsClient NuGet). For each expected `DnsRecord`:
  - TXT records: `QueryAsync(name, QueryType.TXT)`. Check if any TXT value contains expected value.
  - CNAME records: `QueryAsync(name, QueryType.CNAME)`. Check if canonical name matches expected value.
  Returns `List<DnsVerificationResult>` with `IsVerified`, `ExpectedValue`, `ActualValue` per record.
- **Dependencies:** DnsClient NuGet, IDnsVerifier interface
- **Test file:** `tests/EaaS.Infrastructure.Tests/Dns/DnsVerifierTests.cs` (with mocked ILookupClient)
- **Time:** 15 min

### File #149: `src/EaaS.Api/Features/Domains/VerifyDomain/VerifyDomainCommand.cs`
- **Story:** US-3.2
- **What it does:** MediatR command: DomainId + TenantId.
- **Dependencies:** MediatR
- **Test file:** N/A
- **Time:** 2 min

### File #150: `src/EaaS.Api/Features/Domains/VerifyDomain/VerifyDomainCommandHandler.cs`
- **Story:** US-3.2
- **What it does:** Fetches domain, verifies each DNS record, updates status.
- **Key patterns:**
  1. Fetch domain by ID (include DnsRecords). If not found: throw `KeyNotFoundException`.
  2. Call `IDnsVerifier.VerifyAsync(domain.DnsRecords)`.
  3. Update each `DnsRecord.IsVerified`, `VerifiedAt`, `ActualValue`.
  4. If all verified: set domain status to `Verified`, set `VerifiedAt`.
  5. If any failed: set domain status to `Failed`.
  6. Save.
- **Dependencies:** IDomainRepository, IDnsVerifier, IUnitOfWork
- **Test file:** `tests/EaaS.Api.Tests/Features/Domains/VerifyDomainCommandHandlerTests.cs`
- **Time:** 15 min

### File #151: `src/EaaS.Api/Features/Domains/VerifyDomain/VerifyDomainEndpoint.cs`
- **Story:** US-3.2
- **What it does:** Maps `POST /api/v1/domains/{id}/verify`. Returns 200.
- **Dependencies:** MediatR
- **Test file:** Covered by integration tests
- **Time:** 5 min

### File #152: `src/EaaS.Api/Mapping/EntityMappingExtensions.cs`
- **Story:** All features
- **What it does:** Manual mapping extension methods: Entity -> DTO for all response types.
- **Key patterns:** Static extension methods: `public static CreateApiKeyResponse ToCreateResponse(this ApiKey entity, string plaintextKey)`, `public static AddDomainResponse ToResponse(this Domain entity)`, etc. One method per mapping needed. No AutoMapper.
- **Dependencies:** All entity and DTO types
- **Test file:** N/A (trivial mappings, tested indirectly via handler tests)
- **Time:** 15 min

**Phase 4 Verification:** Can create an API key via seed command. Can use that key to authenticate. Can create a second key via API. Can add a domain and receive DNS records. Can verify a domain (against real DNS or mocked). Rate limiting returns 429 after threshold.

---

## 7. Phase 5: Email Sending (US-1.1, US-2.1, US-2.2, US-1.3) -- Target 4h

### US-2.1: Create Template (build before email sending so US-1.3 can use it)

### File #153: `src/EaaS.Infrastructure/Templating/FluidTemplateRenderer.cs`
- **Story:** US-2.1
- **What it does:** Implements `ITemplateRenderer`. Renders Liquid templates via Fluid.
- **Key patterns:**
  ```csharp
  var parser = new FluidParser();
  if (parser.TryParse(template.HtmlBody, out var htmlTemplate, out var error))
  {
      var context = new TemplateContext(model);
      var html = await htmlTemplate.RenderAsync(context);
  }
  ```
  Validates Liquid syntax on template creation (throws `TemplateRenderException` on syntax error). Caches parsed templates in `FluidParser` (thread-safe).
- **Dependencies:** Fluid.Core NuGet, ITemplateRenderer interface
- **Test file:** `tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs`
- **Time:** 15 min

### File #154: `src/EaaS.Api/Features/Templates/CreateTemplate/CreateTemplateRequest.cs`
- **Story:** US-2.1
- **What it does:** Request DTO: Name, SubjectTemplate, HtmlBody, TextBody, VariablesSchema.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #155: `src/EaaS.Api/Features/Templates/CreateTemplate/CreateTemplateResponse.cs`
- **Story:** US-2.1
- **What it does:** Response DTO: TemplateId, Name, Version, CreatedAt.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 2 min

### File #156: `src/EaaS.Api/Features/Templates/CreateTemplate/CreateTemplateValidator.cs`
- **Story:** US-2.1
- **What it does:** Validates: Name required, max 255, unique among active templates. HtmlBody required, max 512KB. SubjectTemplate required, max 1024 chars. Liquid syntax check on both.
- **Key patterns:** Uniqueness check: inject `ITemplateRepository` and use `MustAsync` to verify name not taken. Liquid syntax check: use `FluidParser.TryParse` in a `Must` rule.
- **Dependencies:** FluentValidation, ITemplateRepository, Fluid.Core
- **Test file:** `tests/EaaS.Api.Tests/Features/Templates/CreateTemplateValidatorTests.cs`
- **Time:** 10 min

### File #157: `src/EaaS.Api/Features/Templates/CreateTemplate/CreateTemplateCommand.cs`
- **Story:** US-2.1
- **Dependencies:** MediatR
- **Time:** 2 min

### File #158: `src/EaaS.Api/Features/Templates/CreateTemplate/CreateTemplateCommandHandler.cs`
- **Story:** US-2.1
- **What it does:** Creates template entity, saves, caches in Redis.
- **Key patterns:** Create `Template` entity with version=1. Generate TemplateId. Save. Cache compiled template in Redis via `RedisTemplateCache.SetAsync`. Return response.
- **Dependencies:** ITemplateRepository, IUnitOfWork, RedisTemplateCache, IdGenerator
- **Test file:** `tests/EaaS.Api.Tests/Features/Templates/CreateTemplateCommandHandlerTests.cs`
- **Time:** 10 min

### File #159: `src/EaaS.Api/Features/Templates/CreateTemplate/CreateTemplateEndpoint.cs`
- **Story:** US-2.1
- **What it does:** Maps `POST /api/v1/templates`. Returns 201.
- **Dependencies:** MediatR
- **Time:** 5 min

### US-2.2: Update Template

### File #160: `src/EaaS.Api/Features/Templates/UpdateTemplate/UpdateTemplateRequest.cs`
- **Story:** US-2.2
- **What it does:** Request DTO: all fields optional (partial update). Name, SubjectTemplate, HtmlBody, TextBody, VariablesSchema.
- **Dependencies:** None
- **Time:** 2 min

### File #161: `src/EaaS.Api/Features/Templates/UpdateTemplate/UpdateTemplateValidator.cs`
- **Story:** US-2.2
- **What it does:** Same validation as create but fields are optional. Liquid syntax check only if field is provided.
- **Dependencies:** FluentValidation
- **Test file:** N/A (lightweight, covered by handler tests)
- **Time:** 5 min

### File #162: `src/EaaS.Api/Features/Templates/UpdateTemplate/UpdateTemplateCommand.cs`
- **Story:** US-2.2
- **Dependencies:** MediatR
- **Time:** 2 min

### File #163: `src/EaaS.Api/Features/Templates/UpdateTemplate/UpdateTemplateCommandHandler.cs`
- **Story:** US-2.2
- **What it does:** Fetches template, applies partial update, increments version, invalidates Redis cache.
- **Key patterns:** Fetch by ID. If not found or deleted: throw. Apply only provided fields. Call `template.Update()` to increment version and set UpdatedAt. Save. Invalidate Redis cache.
- **Dependencies:** ITemplateRepository, IUnitOfWork, RedisTemplateCache
- **Test file:** `tests/EaaS.Api.Tests/Features/Templates/UpdateTemplateCommandHandlerTests.cs`
- **Time:** 10 min

### File #164: `src/EaaS.Api/Features/Templates/UpdateTemplate/UpdateTemplateEndpoint.cs`
- **Story:** US-2.2
- **What it does:** Maps `PUT /api/v1/templates/{id}`. Returns 200.
- **Dependencies:** MediatR
- **Time:** 5 min

### US-1.1: Send Single Email (THE critical path)

### File #165: `src/EaaS.Api/Features/Emails/SendEmail/SendEmailRequest.cs`
- **Story:** US-1.1
- **What it does:** Request DTO matching Architecture section 4.1 exactly.
- **Key patterns:** Record with: From (EmailAddressDto), To (List<EmailAddressDto>), Cc, Bcc, Subject, HtmlBody, TextBody, TemplateId, Variables (Dictionary<string,object>), Tags, Metadata, TrackOpens, TrackClicks. `EmailAddressDto` is a nested record with Email and Name.
- **Dependencies:** None
- **Test file:** N/A
- **Time:** 5 min

### File #166: `src/EaaS.Api/Features/Emails/SendEmail/SendEmailResponse.cs`
- **Story:** US-1.1
- **What it does:** Response DTO: MessageId, Status, QueuedAt.
- **Dependencies:** None
- **Time:** 2 min

### File #167: `src/EaaS.Api/Features/Emails/SendEmail/SendEmailValidator.cs`
- **Story:** US-1.1
- **What it does:** Comprehensive validation per Architecture section 4.1 validation rules.
- **Key patterns:**
  - `from.email` required, valid email format
  - `to` at least 1, max 50
  - Combined to + cc + bcc max 50
  - Either (subject + html_body) or template_id, not both
  - All email addresses RFC 5322 validated
  - Subject max 998 chars
- **Dependencies:** FluentValidation
- **Test file:** `tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs`
- **Time:** 15 min

### File #168: `src/EaaS.Api/Features/Emails/SendEmail/SendEmailCommand.cs`
- **Story:** US-1.1
- **What it does:** MediatR command wrapping the request + TenantId + ApiKeyId from claims.
- **Dependencies:** MediatR
- **Time:** 2 min

### File #169: `src/EaaS.Api/Features/Emails/SendEmail/SendEmailCommandHandler.cs`
- **Story:** US-1.1
- **What it does:** The critical handler. Validates domain, checks suppression, saves email, publishes to queue.
- **Key patterns:**
  1. Extract domain from `from.email` (split on @).
  2. Check domain is verified via `IDomainRepository.GetByDomainNameAsync`. If not verified: throw `DomainNotVerifiedException`.
  3. Check API key allowed_domains (if restricted). If from-domain not in list: throw 403.
  4. Check each `to` recipient against suppression: `ISuppressionCache.IsSuppressedAsync`. If any suppressed: throw `RecipientSuppressedException`.
  5. If `template_id` provided: verify template exists via `ITemplateRepository.GetByIdAsync`. If not: throw `TemplateNotFoundException`.
  6. Generate `MessageId` via `IdGenerator.NewMessageId()`.
  7. Create `Email` entity with status `Queued`, all fields mapped from request.
  8. Create `EmailEvent` with type `Queued`.
  9. Save to DB via `IUnitOfWork.SaveChangesAsync`.
  10. Publish `SendEmailMessage` to queue via `IMessagePublisher.PublishSendEmailAsync`.
  11. Return `SendEmailResponse(messageId, "queued", now)`.
- **Dependencies:** IDomainRepository, ISuppressionCache, ITemplateRepository, IEmailRepository, IUnitOfWork, IMessagePublisher, IDateTimeProvider, IdGenerator
- **Test file:** `tests/EaaS.Api.Tests/Features/Emails/SendEmailCommandHandlerTests.cs`
- **Time:** 25 min

### File #170: `src/EaaS.Api/Features/Emails/SendEmail/SendEmailEndpoint.cs`
- **Story:** US-1.1
- **What it does:** Maps `POST /api/v1/emails/send`. Returns 202 Accepted.
- **Key patterns:** Extracts TenantId and ApiKeyId from claims. Sends command via MediatR. Returns `Results.Accepted(null, ApiResponse.Success(response))`.
- **Dependencies:** MediatR
- **Test file:** Covered by integration tests
- **Time:** 5 min

### File #171: `src/EaaS.Api/Features/Emails/GetEmail/GetEmailQuery.cs`
- **Story:** US-1.1 (AC #9)
- **What it does:** MediatR query: MessageId + TenantId.
- **Dependencies:** MediatR
- **Time:** 2 min

### File #172: `src/EaaS.Api/Features/Emails/GetEmail/GetEmailResponse.cs`
- **Story:** US-1.1
- **What it does:** Full email detail response matching Architecture section 4.3.
- **Dependencies:** None
- **Time:** 5 min

### File #173: `src/EaaS.Api/Features/Emails/GetEmail/GetEmailQueryHandler.cs`
- **Story:** US-1.1
- **What it does:** Fetches email by message_id with events.
- **Key patterns:** `IEmailRepository.GetByMessageIdAsync(messageId)`. Include events. If not found: throw `KeyNotFoundException`. Map to response.
- **Dependencies:** IEmailRepository
- **Test file:** `tests/EaaS.Api.Tests/Features/Emails/GetEmailQueryHandlerTests.cs`
- **Time:** 10 min

### File #174: `src/EaaS.Api/Features/Emails/GetEmail/GetEmailEndpoint.cs`
- **Story:** US-1.1
- **What it does:** Maps `GET /api/v1/emails/{id}`. Returns 200.
- **Dependencies:** MediatR
- **Time:** 5 min

### Worker Consumer (the other half of US-1.1)

### File #175: `src/EaaS.Infrastructure/Email/SesEmailDeliveryService.cs`
- **Story:** US-1.1
- **What it does:** Implements `IEmailDeliveryService`. Sends email via SES v2 SDK.
- **Key patterns:**
  ```csharp
  var request = new SendEmailRequest
  {
      FromEmailAddress = $"{fromName} <{fromEmail}>",
      Destination = new Destination
      {
          ToAddresses = toList,
          CcAddresses = ccList,
          BccAddresses = bccList
      },
      Content = new EmailContent
      {
          Simple = new Message
          {
              Subject = new Content { Data = subject },
              Body = new Body
              {
                  Html = new Content { Data = htmlBody },
                  Text = textBody != null ? new Content { Data = textBody } : null
              }
          }
      }
  };
  var response = await _sesClient.SendEmailAsync(request, ct);
  return response.MessageId;
  ```
  Uses Simple content (not Raw) for Sprint 1 since we do not have attachments. Switch to Raw in Sprint 2.
- **Dependencies:** AWSSDK.SimpleEmailV2, SesOptions
- **Test file:** N/A (mocked in consumer tests)
- **Time:** 15 min

### File #176: `src/EaaS.Worker/Consumers/SendEmailConsumer.cs`
- **Story:** US-1.1
- **What it does:** MassTransit consumer for `SendEmailMessage`. The core email processing pipeline.
- **Key patterns:**
  1. Receive `SendEmailMessage` from queue.
  2. If `TemplateId` set: fetch template from DB, render via `ITemplateRenderer.RenderAsync`. Use rendered HTML/text.
  3. If no template: use inline `HtmlBody`/`TextBody` from message.
  4. Call `IEmailDeliveryService.SendAsync` to send via SES.
  5. Update email status in DB: `Sent` with `SesMessageId` and `SentAt`.
  6. Create `EmailEvent` with type `Sent`.
  7. Save via `IUnitOfWork`.
  8. On failure: update status to `Failed`, set `ErrorMessage`, create `Failed` event, rethrow (MassTransit handles retry/DLQ).

  Implements `IConsumer<SendEmailMessage>`. Constructor-injected dependencies.
- **Dependencies:** IEmailRepository, IEmailDeliveryService, ITemplateRenderer, ITemplateRepository, IUnitOfWork, IDateTimeProvider
- **Test file:** `tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs`
- **Time:** 30 min

### File #177: Update `src/EaaS.Worker/Program.cs`
- **Story:** US-1.1
- **What it does:** Full Worker setup with MassTransit, EF Core, Serilog, and all dependencies.
- **Key patterns:**
  ```csharp
  var builder = Host.CreateApplicationBuilder(args);
  builder.Services.AddInfrastructure(builder.Configuration);
  builder.UseSerilog();
  var host = builder.Build();
  await host.RunAsync();
  ```
  MassTransit consumer registration is handled by `AddInfrastructure` (via `MassTransitConfiguration`).
- **Dependencies:** Infrastructure DI, all consumer classes
- **Test file:** N/A
- **Time:** 10 min

### US-1.3: Template-based Send

### File #178: No new files needed.
- **Story:** US-1.3
- **What it does:** Template-based sending is already handled by the `SendEmailCommandHandler` (checks template_id and validates template exists) and `SendEmailConsumer` (renders template if template_id present). The validation in `SendEmailValidator` already handles the either-subject+html_body-or-template_id logic.
- **Key patterns:** The template rendering path in `SendEmailConsumer` (File #176) handles this:
  - If `TemplateId` is set: load template, validate variables against `VariablesSchema` (if present), render with Fluid.
  - Variables schema validation: parse `VariablesSchema` as JSON Schema, validate `Variables` dict against it. Use simple key-presence check for Sprint 1 (full JSON Schema validation is over-engineering for MVP).
- **Dependencies:** All existing files from US-2.1 and US-1.1
- **Test file:** Additional test cases in `SendEmailConsumerTests.cs` for template rendering path
- **Time:** 15 min (additional test cases + template rendering logic in consumer)

### Update `src/EaaS.Api/Program.cs` -- Final Assembly

### File #179: Complete `src/EaaS.Api/Program.cs`
- **Story:** All API stories
- **What it does:** Full Program.cs with all middleware, endpoint mapping, DI, Serilog, health checks, Swagger.
- **Key patterns:**
  ```csharp
  var builder = WebApplication.CreateBuilder(args);

  // Handle seed commands
  if (args.Length > 0 && args[0] == "seed") { /* seed logic */ }

  // Serilog
  Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
  builder.Host.UseSerilog();

  // DI
  builder.Services.AddInfrastructure(builder.Configuration);
  builder.Services.AddApiServices();
  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen();

  var app = builder.Build();

  // Middleware pipeline (order matters)
  app.UseExceptionHandler(o => {});  // Uses IExceptionHandler
  app.UseMiddleware<RequestLoggingMiddleware>();
  app.UseAuthentication();
  app.UseAuthorization();
  app.UseMiddleware<RateLimitingMiddleware>();

  // Apply migrations in Development
  if (app.Environment.IsDevelopment())
  {
      using var scope = app.Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<EaaSDbContext>();
      await db.Database.MigrateAsync();
  }

  // Map endpoints
  HealthEndpoint.Map(app);
  CreateApiKeyEndpoint.Map(app);
  RevokeApiKeyEndpoint.Map(app);
  AddDomainEndpoint.Map(app);
  VerifyDomainEndpoint.Map(app);
  CreateTemplateEndpoint.Map(app);
  UpdateTemplateEndpoint.Map(app);
  SendEmailEndpoint.Map(app);
  GetEmailEndpoint.Map(app);

  // Swagger (dev only)
  if (app.Environment.IsDevelopment())
  {
      app.UseSwagger();
      app.UseSwaggerUI();
  }

  await app.RunAsync();
  ```
- **Dependencies:** All middleware, endpoints, DI classes
- **Test file:** N/A
- **Time:** 15 min

**Phase 5 Verification:** Send an email via `POST /api/v1/emails/send` with a valid API key and verified domain. Email appears in the queue. Worker consumes it, sends via SES (or logs the attempt if in sandbox). Status updates in DB. `GET /api/v1/emails/{id}` returns the status. Template creation and template-based send also work.

---

## 8. Phase 6: Testing & Polish -- Target 2h

### Test Files (TDD -- these are written BEFORE implementation, but listed here for the phase where comprehensive test coverage is verified)

#### Unit Tests: API Handlers

| # | File | Tests | Time |
|---|------|-------|------|
| 180 | `tests/EaaS.Api.Tests/Features/ApiKeys/CreateApiKeyCommandHandlerTests.cs` | Valid create, duplicate name handling | 10 min |
| 181 | `tests/EaaS.Api.Tests/Features/ApiKeys/CreateApiKeyValidatorTests.cs` | Empty name, too long, valid | 5 min |
| 182 | `tests/EaaS.Api.Tests/Features/ApiKeys/RevokeApiKeyCommandHandlerTests.cs` | Valid revoke, not found, already revoked | 8 min |
| 183 | `tests/EaaS.Api.Tests/Features/Domains/AddDomainCommandHandlerTests.cs` | Valid add, duplicate, SES call verification | 10 min |
| 184 | `tests/EaaS.Api.Tests/Features/Domains/AddDomainValidatorTests.cs` | Invalid domain format, valid | 5 min |
| 185 | `tests/EaaS.Api.Tests/Features/Domains/VerifyDomainCommandHandlerTests.cs` | All pass, partial fail, not found | 10 min |
| 186 | `tests/EaaS.Api.Tests/Features/Templates/CreateTemplateCommandHandlerTests.cs` | Valid create, Redis cache set | 8 min |
| 187 | `tests/EaaS.Api.Tests/Features/Templates/CreateTemplateValidatorTests.cs` | Liquid syntax error, name too long, valid | 8 min |
| 188 | `tests/EaaS.Api.Tests/Features/Templates/UpdateTemplateCommandHandlerTests.cs` | Valid update, version increment, not found, cache invalidate | 8 min |
| 189 | `tests/EaaS.Api.Tests/Features/Emails/SendEmailCommandHandlerTests.cs` | Valid send, suppressed recipient, unverified domain, missing template, domain not in allowed list | 15 min |
| 190 | `tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs` | All validation rules covered | 12 min |
| 191 | `tests/EaaS.Api.Tests/Features/Emails/GetEmailQueryHandlerTests.cs` | Found, not found | 5 min |

#### Unit Tests: Middleware

| # | File | Tests | Time |
|---|------|-------|------|
| 192 | `tests/EaaS.Api.Tests/Middleware/ApiKeyAuthenticationHandlerTests.cs` | Valid key, invalid key, revoked key, missing header, Redis cache hit | 12 min |
| 193 | `tests/EaaS.Api.Tests/Middleware/RateLimitingMiddlewareTests.cs` | Under limit, at limit, over limit, Retry-After header | 8 min |
| 194 | `tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs` | Each domain exception maps to correct status code | 8 min |
| 195 | `tests/EaaS.Api.Tests/Behaviors/ValidationBehaviorTests.cs` | Valid request passes through, invalid request throws | 5 min |

#### Unit Tests: Worker

| # | File | Tests | Time |
|---|------|-------|------|
| 196 | `tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs` | Successful send, SES failure (retried), template render, template not found, status update | 20 min |

#### Infrastructure Tests

| # | File | Tests | Time |
|---|------|-------|------|
| 197 | `tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs` | Variable substitution, missing vars, syntax errors, max size | 10 min |
| 198 | `tests/EaaS.Infrastructure.Tests/Caching/RedisSuppressionCacheTests.cs` | Add, check exists, check not exists (Testcontainers Redis) | 8 min |
| 199 | `tests/EaaS.Infrastructure.Tests/Persistence/ApiKeyRepositoryTests.cs` | CRUD, GetByKeyHash, status filter (Testcontainers PostgreSQL) | 10 min |
| 200 | `tests/EaaS.Infrastructure.Tests/Persistence/EmailRepositoryTests.cs` | Add, GetByMessageId, ListAsync with filters (Testcontainers PostgreSQL) | 12 min |

#### Integration Tests (E2E)

| # | File | Tests | Time |
|---|------|-------|------|
| 201 | `tests/EaaS.Integration.Tests/Scenarios/SendEmailE2ETests.cs` | Full flow: create key -> add domain -> send email -> verify DB status. Uses WebApplicationFactory + Testcontainers. | 20 min |

### Docker Compose Verification

| # | Task | Time |
|---|------|------|
| 202 | Run `docker compose build` -- verify all images build | 10 min |
| 203 | Run `docker compose up -d` -- verify all services healthy | 5 min |
| 204 | Manual smoke test: seed API key, add domain, send email | 10 min |
| 205 | Check structured logs: `docker compose logs api --tail 50` | 5 min |

**Phase 6 Verification:** All unit tests pass. Integration tests pass with Testcontainers. Docker Compose starts cleanly. Manual end-to-end test sends an email successfully (or demonstrates the full flow up to SES sandbox limitation).

---

## 9. Risk Mitigation

### 9.1 AWS SES Sandbox Mode

**Problem:** New SES accounts are in sandbox mode -- can only send to verified email addresses.

**Mitigation:**
1. Request production access immediately (submit SES sending limit increase request via AWS console). Takes 1-3 business days.
2. For Sprint 1 demo: pre-verify 2-3 test email addresses in SES console (personal Gmail, etc.).
3. The code works the same in sandbox vs production -- only the recipient restriction changes.
4. Document the sandbox limitation in the README and acceptance test notes.

### 9.2 Docker Compose Issues

**Problem:** Resource limits, networking, or health check issues.

**Mitigation:**
1. Build and test Docker Compose early (Phase 1).
2. If health checks fail: increase timeouts and retries in compose file.
3. If memory limits cause OOM: increase limits or reduce RabbitMQ memory (it is the most memory-hungry).
4. Fallback: run PostgreSQL, Redis, and RabbitMQ via Docker but run .NET services directly on host (`dotnet run`). This bypasses container networking issues while keeping infrastructure containerized.

### 9.3 Behind Schedule -- Cut List

Reference Gate 1 review section 7 cut list. In priority order of what to drop:

1. **US-2.2 (Update Template) -- 2 pts.** Saves ~45 min. Templates can be created but not updated. Create new ones instead.
2. **US-1.3 (Template-based Send) -- 3 pts.** Saves ~1.5h. Send with inline HTML only. Templates exist but are not wired to send.
3. **US-2.1 (Create Template) -- 3 pts.** Saves ~1h. Only cut if US-1.3 is also cut.
4. **US-3.2 (Verify Domain DNS) -- 3 pts.** Saves ~1.5h. Verify domain manually in SES console.
5. **US-5.3 (Revoke API Key) -- 2 pts.** Saves ~30 min. Not critical for solo operator.

**Total recoverable time: ~5.25 hours.**

### 9.4 NSubstitute vs Moq

**Problem:** Architecture lists Moq in the NuGet packages.

**Mitigation:** Replace Moq with NSubstitute in `Directory.Packages.props`. NSubstitute is safer (no SponsorLink telemetry controversy), has cleaner syntax, and achieves the same mocking capabilities. Update the package reference:
```xml
<!-- Remove -->
<PackageVersion Include="Moq" Version="4.20.70" />
<!-- Add -->
<PackageVersion Include="NSubstitute" Version="5.1.0" />
```

### 9.5 EF Core PostgreSQL Enum Mapping

**Problem:** PostgreSQL enums require special handling with EF Core + Npgsql.

**Mitigation:** In `EaaSDbContext.OnModelCreating`, register all enums:
```csharp
modelBuilder.HasPostgresEnum<EmailStatus>();
modelBuilder.HasPostgresEnum<EventType>();
// etc.
```
And in `AddDbContext` configuration:
```csharp
NpgsqlConnection.GlobalTypeMapper.MapEnum<EmailStatus>();
// etc.
```
This must be done before any queries. Register in a static constructor or startup.

### 9.6 MassTransit Delayed Redelivery Requires RabbitMQ Plugin

**Problem:** `UseDelayedRedelivery` requires the `rabbitmq_delayed_message_exchange` plugin installed on RabbitMQ.

**Mitigation:** Two options:
1. **Install the plugin** in the RabbitMQ Docker image (custom Dockerfile extending `rabbitmq:3.13-management-alpine` with `rabbitmq-plugins enable rabbitmq_delayed_message_exchange`).
2. **Use `UseScheduledRedelivery`** with MassTransit's built-in scheduler instead, which uses a simpler approach.
3. **Simplest for Sprint 1:** Use `UseMessageRetry` with exponential intervals (1s, 5s, 30s) for in-memory retry. If all retries fail, message goes to `_error` queue. This avoids the plugin dependency entirely. The delayed redelivery (minutes between retries) can be added in Sprint 2 with the plugin.

**Decision:** Use `UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)))` for Sprint 1. This is immediate in-memory retry, no plugin needed. Add delayed redelivery in Sprint 2.

---

## 10. Complete File Index

Total files: 205 items (files + verification tasks)

| # | File Path | Story | Phase | Time |
|---|-----------|-------|-------|------|
| 1 | `eaas.sln` | US-0.1 | 1 | 5m |
| 2 | `Directory.Build.props` | US-0.1 | 1 | 2m |
| 3 | `Directory.Packages.props` | US-0.1 | 1 | 5m |
| 4 | `src/EaaS.Domain/EaaS.Domain.csproj` | US-0.1 | 1 | 2m |
| 5 | `src/EaaS.Shared/EaaS.Shared.csproj` | US-0.1 | 1 | 2m |
| 6 | `src/EaaS.Infrastructure/EaaS.Infrastructure.csproj` | US-0.1 | 1 | 3m |
| 7 | `src/EaaS.Api/EaaS.Api.csproj` | US-0.1 | 1 | 3m |
| 8 | `src/EaaS.Worker/EaaS.Worker.csproj` | US-0.1 | 1 | 2m |
| 9 | `src/EaaS.WebhookProcessor/EaaS.WebhookProcessor.csproj` | US-0.1 | 1 | 2m |
| 10 | `dashboard/package.json` | US-0.1 | 1 | 2m |
| 11 | `tests/EaaS.Api.Tests/EaaS.Api.Tests.csproj` | US-0.1 | 1 | 2m |
| 12 | `tests/EaaS.Worker.Tests/EaaS.Worker.Tests.csproj` | US-0.1 | 1 | 2m |
| 13 | `tests/EaaS.Infrastructure.Tests/EaaS.Infrastructure.Tests.csproj` | US-0.1 | 1 | 2m |
| 14 | `tests/EaaS.Integration.Tests/EaaS.Integration.Tests.csproj` | US-0.1 | 1 | 2m |
| 15 | `src/EaaS.Api/Program.cs` (stub) | US-0.1 | 1 | 5m |
| 16 | `src/EaaS.Worker/Program.cs` (stub) | US-0.1 | 1 | 5m |
| 17 | `src/EaaS.WebhookProcessor/Program.cs` (stub) | US-0.1 | 1 | 5m |
| 18 | `dashboard/src/app/page.tsx` (stub) | US-0.1 | 1 | 5m |
| 19 | `src/EaaS.Api/Dockerfile` | US-0.1 | 1 | 10m |
| 20 | `src/EaaS.Worker/Dockerfile` | US-0.1 | 1 | 5m |
| 21 | `src/EaaS.WebhookProcessor/Dockerfile` | US-0.1 | 1 | 5m |
| 22 | `dashboard/Dockerfile` | US-0.1 | 1 | 5m |
| 23 | `docker-compose.yml` | US-0.1 | 1 | 10m |
| 24 | `docker-compose.override.yml` | US-0.1 | 1 | 10m |
| 25 | `nginx/nginx.conf` | US-0.1 | 1 | 5m |
| 26 | `scripts/init-db.sql` | US-0.1 | 1 | 5m |
| 27 | `scripts/seed-data.sql` | US-0.1 | 1 | 5m |
| 28 | `.env.example` | US-0.1 | 1 | 5m |
| 29 | `.gitignore` | US-0.1 | 1 | 2m |
| 30 | `.editorconfig` | US-0.1 | 1 | 2m |
| 31-37 | Options classes (7 files) | US-0.6 | 2 | 15m |
| 38-39 | appsettings.json + Development | US-0.6 | 2 | 8m |
| 40-46 | Enums (7 files) | US-0.2 | 2 | 8m |
| 47-52 | Value objects (6 files) | US-0.2 | 2 | 16m |
| 53-61 | Entities (9 files) | US-0.2 | 2 | 40m |
| 62-68 | Exceptions (7 files) | US-0.2 | 2 | 8m |
| 69-82 | Interfaces (14 files) | US-0.2 | 2 | 34m |
| 83-93 | Shared models/constants/helpers (11 files) | US-0.2 | 2 | 25m |
| 94 | `EaaSDbContext.cs` | US-0.2 | 2 | 20m |
| 95-103 | EF Core configurations (9 files) | US-0.2 | 2 | 40m |
| 104 | `UnitOfWork.cs` | US-0.2 | 2 | 3m |
| 105 | Initial EF Core migration | US-0.2 | 2 | 10m |
| 106-108 | Redis caching (3 files) | US-0.3 | 3 | 33m |
| 109-112 | MassTransit messaging (4 files) | US-0.3 | 3 | 31m |
| 113-115 | Health checks (3 files) | US-0.4 | 3 | 28m |
| 116-118 | Logging/error middleware (3 files) | US-0.5 | 3 | 30m |
| 119-122 | DI + ValidationBehavior + DateTimeProvider | US-0.3-0.6 | 3 | 42m |
| 123-128 | Repositories (6 files) | US-0.2 | 4 | 60m |
| 129-130 | Auth + rate limiting middleware | US-5.1 | 4 | 35m |
| 131-137 | Create API Key feature (7 files) | US-5.1 | 4 | 46m |
| 138-140 | Revoke API Key feature (3 files) | US-5.3 | 4 | 17m |
| 141-147 | Add Domain feature (7 files) | US-3.1 | 4 | 52m |
| 148-151 | Verify Domain feature (4 files) | US-3.2 | 4 | 37m |
| 152 | Entity mapping extensions | All | 4 | 15m |
| 153-159 | Create Template feature (7 files) | US-2.1 | 5 | 46m |
| 160-164 | Update Template feature (5 files) | US-2.2 | 5 | 24m |
| 165-174 | Send Email + Get Email features (10 files) | US-1.1 | 5 | 76m |
| 175-177 | SES service + Worker consumer + Worker Program | US-1.1 | 5 | 55m |
| 178 | Template-based send (no new file, test additions) | US-1.3 | 5 | 15m |
| 179 | Final Program.cs assembly | All | 5 | 15m |
| 180-201 | Test files (22 files) | All | 6* | ~180m |
| 202-205 | Docker verification tasks | All | 6 | 30m |

*Note: Test files are written BEFORE implementation (TDD) but are listed in Phase 6 for verification purposes. In practice, each test file is written immediately before the corresponding implementation file.

---

## TDD Execution Order

For each feature, the actual coding order is:

1. Write the test file first (failing tests)
2. Write the minimum implementation to pass
3. Refactor if needed
4. Move to next feature

Example for US-5.1 (Create API Key):
1. Write `CreateApiKeyValidatorTests.cs` (failing)
2. Write `CreateApiKeyValidator.cs` (passes)
3. Write `CreateApiKeyCommandHandlerTests.cs` (failing)
4. Write `CreateApiKeyCommandHandler.cs` (passes)
5. Wire up endpoint + manual smoke test

---

**Total estimated implementation time: 16 hours active coding + 8 hours buffer = 24 hours.**

**This plan is exhaustive. Every file, every class, every pattern decision is documented. Ready for Gate 2 review.**
