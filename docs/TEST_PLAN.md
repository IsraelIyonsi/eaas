# EaaS - Sprint 1 Test Plan

**Version:** 1.0
**Date:** 2026-03-27
**Author:** Senior QA Engineer
**Sprint:** 1 (MVP)
**Status:** Ready for TDD Red Phase

---

## Table of Contents

1. [Test Strategy Overview](#1-test-strategy-overview)
2. [Test Infrastructure Setup](#2-test-infrastructure-setup)
3. [Coverage Targets](#3-coverage-targets)
4. [Test Cases by Story](#4-test-cases-by-story)
5. [Sprint 1 Test Summary](#5-sprint-1-test-summary)

---

## 1. Test Strategy Overview

### Approach
TDD mandatory: every test case defined here will be implemented as a **failing test first** (RED phase), before any production code is written. Tests are the specification.

### Test Pyramid

| Layer | Framework | Purpose | Run Time |
|-------|-----------|---------|----------|
| **Unit** | xUnit + FluentAssertions + NSubstitute | Isolated class/method behavior | < 5s total |
| **Integration** | xUnit + Testcontainers + WebApplicationFactory | Component collaboration with real deps | < 60s total |
| **E2E** | xUnit + Testcontainers + MassTransit InMemoryTestHarness | Full flow from API to Worker | < 120s total |

### Naming Convention
```
Should_{ExpectedBehavior}_When_{Condition}
```
Example: `Should_ReturnAccepted_When_ValidEmailSent`

### Test Project Mapping

| Test Project | Source Project(s) | Test Type |
|-------------|-------------------|-----------|
| `tests/EaaS.Api.Tests/` | EaaS.Api, EaaS.Domain | Unit |
| `tests/EaaS.Worker.Tests/` | EaaS.Worker | Unit |
| `tests/EaaS.Infrastructure.Tests/` | EaaS.Infrastructure | Unit + Integration |
| `tests/EaaS.Integration.Tests/` | All | Integration + E2E |

---

## 2. Test Infrastructure Setup

### 2.1 Testcontainers Configuration

**File:** `tests/EaaS.Integration.Tests/Fixtures/PostgresContainerFixture.cs`

```
- PostgreSQL 16 container via Testcontainers.PostgreSql
- Runs EF Core migrations on startup
- Provides connection string to WebApplicationFactory
- Disposed after test class completes
```

**File:** `tests/EaaS.Integration.Tests/Fixtures/RedisContainerFixture.cs`

```
- Redis 7 container via Testcontainers.Redis (Testcontainers generic container)
- Provides connection string for StackExchange.Redis
- Used for suppression cache and rate limiting integration tests
```

**File:** `tests/EaaS.Integration.Tests/Fixtures/RabbitMqContainerFixture.cs`

```
- RabbitMQ 3.13 container via Testcontainers.RabbitMq
- MassTransit configured against real broker for E2E tests
- Virtual host /eaas created automatically
```

**File:** `tests/EaaS.Integration.Tests/Fixtures/IntegrationTestFixture.cs`

```
- Combines all three containers into a single IAsyncLifetime fixture
- Implements ICollectionFixture<IntegrationTestFixture> for shared container lifecycle
- Containers start once per test collection, not per test class
```

### 2.2 WebApplicationFactory Configuration

**File:** `tests/EaaS.Integration.Tests/Fixtures/EaaSWebApplicationFactory.cs`

```
- Extends WebApplicationFactory<Program>
- Replaces connection strings with Testcontainers endpoints
- Replaces IEmailDeliveryService with FakeSesService
- Replaces IDnsVerifier with FakeDnsVerifier
- Disables HTTPS redirection for test HTTP client
- Seeds default tenant and bootstrap API key
- Configures MassTransit InMemoryTestHarness for unit-level integration tests
```

### 2.3 MassTransit Test Harness

**File:** `tests/EaaS.Worker.Tests/Fixtures/MassTransitTestHarnessFixture.cs`

```
- MassTransit InMemoryTestHarness
- Registers SendEmailConsumer
- Provides IPublishEndpoint for publishing test messages
- Asserts message consumption and fault handling
```

### 2.4 AWS SES Mock

**File:** `tests/EaaS.Integration.Tests/Fakes/FakeSesService.cs`

```
- Implements IEmailDeliveryService
- Records all sent emails in an in-memory list
- Configurable: throw on specific addresses for simulating SES failures
- Returns fake SES Message-ID
- Thread-safe for concurrent consumer tests
```

### 2.5 DNS Verifier Fake

**File:** `tests/EaaS.Integration.Tests/Fakes/FakeDnsVerifier.cs`

```
- Implements IDnsVerifier
- Configurable per-domain verification results
- Default: all records pass
- Can simulate partial failures (e.g., SPF passes, DKIM fails)
```

### 2.6 Test Data Builders

**File:** `tests/EaaS.Integration.Tests/Builders/`

```
SendEmailRequestBuilder.cs    - Fluent builder for valid SendEmailRequest with sensible defaults
CreateTemplateRequestBuilder.cs - Fluent builder for valid CreateTemplateRequest
AddDomainRequestBuilder.cs    - Fluent builder for valid AddDomainRequest
CreateApiKeyRequestBuilder.cs - Fluent builder for valid CreateApiKeyRequest
EmailEntityBuilder.cs         - Fluent builder for Email domain entity (DB seeding)
TemplateEntityBuilder.cs      - Fluent builder for Template domain entity
DomainEntityBuilder.cs        - Fluent builder for Domain domain entity
ApiKeyEntityBuilder.cs        - Fluent builder for ApiKey domain entity
```

### 2.7 Test Helpers

**File:** `tests/EaaS.Integration.Tests/Helpers/`

```
AuthenticatedHttpClient.cs   - Extension: adds Bearer token header to HttpClient
TestApiKeySeeder.cs          - Seeds a known API key (hash + plaintext) for auth tests
JsonHelper.cs                - Deserialize helpers with snake_case naming policy
```

---

## 3. Coverage Targets

| Category | Target | Rationale |
|----------|--------|-----------|
| **Overall** | 80%+ | Industry standard for production-grade API |
| **API Endpoints** | 100% happy paths, 80% error paths | Every endpoint must have at least one passing and one failing test |
| **FluentValidation Validators** | 100% | Validators are the first line of defense; every rule must be tested |
| **MediatR Handlers** | 90%+ | Core business logic lives here |
| **MassTransit Consumers** | 100% happy paths | Message processing must be reliable |
| **Domain Entities** | 90%+ | Value objects and entity behavior |
| **Infrastructure** | 70%+ | Repositories, caching, templating |

---

## 4. Test Cases by Story

---

### US-0.1: Project Scaffolding and Docker Compose Setup

> Focus: Verify solution structure compiles, Docker Compose starts all services, and services communicate.

---

**TC-0.1-01: Should_CompileSuccessfully_When_SolutionBuilt**
```
Type: Unit
Priority: P0
Preconditions: Solution file exists with all project references
Steps:
  1. Run `dotnet build eaas.sln`
Expected Result: Build succeeds with zero errors and zero warnings treated as errors
Test File: (CI pipeline verification -- not a code test)
```

**TC-0.1-02: Should_ResolveAllServices_When_DIContainerBuilt**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory configured
Steps:
  1. Build the WebApplicationFactory host
  2. Resolve all registered services from the DI container
Expected Result: No unresolved dependency exceptions; all required services registered
Test File: tests/EaaS.Integration.Tests/Infrastructure/DependencyInjectionTests.cs
```

**TC-0.1-03: Should_StartSuccessfully_When_AllServicesHealthy**
```
Type: Integration
Priority: P0
Preconditions: Testcontainers for Postgres, Redis, RabbitMQ running
Steps:
  1. Create WebApplicationFactory with Testcontainer connection strings
  2. Create HttpClient
  3. Send GET /health
Expected Result: Response is 200 OK with all components reporting healthy
Test File: tests/EaaS.Integration.Tests/Infrastructure/StartupTests.cs
```

---

### US-0.2: PostgreSQL Schema and Migration Setup

> Focus: Verify EF Core migrations create all tables, indexes, and constraints correctly.

---

**TC-0.2-01: Should_CreateAllTables_When_MigrationApplied**
```
Type: Integration
Priority: P0
Preconditions: Testcontainers PostgreSQL running, clean database
Steps:
  1. Apply EF Core migrations via DbContext.Database.MigrateAsync()
  2. Query information_schema.tables for all expected tables
Expected Result: Tables exist: tenants, api_keys, domains, dns_records, templates, emails, email_events, suppression_list, webhooks, dashboard_users
Test File: tests/EaaS.Infrastructure.Tests/Persistence/MigrationTests.cs
```

**TC-0.2-02: Should_HaveUuidPrimaryKeys_When_TablesCreated**
```
Type: Integration
Priority: P1
Preconditions: Migrations applied
Steps:
  1. Query information_schema.columns for primary key columns
  2. Verify type is uuid for all entity tables
Expected Result: All primary keys are UUID type
Test File: tests/EaaS.Infrastructure.Tests/Persistence/MigrationTests.cs
```

**TC-0.2-03: Should_HaveAuditColumns_When_TablesCreated**
```
Type: Integration
Priority: P1
Preconditions: Migrations applied
Steps:
  1. Query information_schema.columns for created_at and updated_at
Expected Result: All entity tables have created_at and updated_at columns of type timestamptz
Test File: tests/EaaS.Infrastructure.Tests/Persistence/MigrationTests.cs
```

**TC-0.2-04: Should_HaveDefaultTenant_When_MigrationApplied**
```
Type: Integration
Priority: P0
Preconditions: Migrations applied with seed data
Steps:
  1. Query tenants table
Expected Result: One tenant with id '00000000-0000-0000-0000-000000000001' and name 'Default'
Test File: tests/EaaS.Infrastructure.Tests/Persistence/MigrationTests.cs
```

**TC-0.2-05: Should_LoadConnectionString_When_EnvironmentVariableSet**
```
Type: Unit
Priority: P0
Preconditions: DatabaseOptions class exists
Steps:
  1. Build configuration with connection string in environment variables
  2. Bind to DatabaseOptions
Expected Result: ConnectionString property is populated correctly
Test File: tests/EaaS.Api.Tests/Configuration/DatabaseOptionsTests.cs
```

---

### US-0.3: Redis and RabbitMQ Configuration

> Focus: Verify Redis and RabbitMQ connections, queue topology, and DLQ configuration.

---

**TC-0.3-01: Should_ConnectToRedis_When_ConnectionStringValid**
```
Type: Integration
Priority: P0
Preconditions: Testcontainers Redis running
Steps:
  1. Create ConnectionMultiplexer with Testcontainer connection string
  2. Execute PING command
Expected Result: Redis responds with PONG
Test File: tests/EaaS.Infrastructure.Tests/Caching/RedisConnectionTests.cs
```

**TC-0.3-02: Should_ConnectToRabbitMQ_When_ConnectionStringValid**
```
Type: Integration
Priority: P0
Preconditions: Testcontainers RabbitMQ running
Steps:
  1. Configure MassTransit with Testcontainer connection string
  2. Start bus
Expected Result: Bus starts without exception; connection established
Test File: tests/EaaS.Infrastructure.Tests/Messaging/RabbitMqConnectionTests.cs
```

**TC-0.3-03: Should_CreateEmailSendQueue_When_BusStarted**
```
Type: Integration
Priority: P0
Preconditions: MassTransit bus started against RabbitMQ Testcontainer
Steps:
  1. Start MassTransit bus
  2. Verify queue exists via RabbitMQ management API or connection inspection
Expected Result: Queue eaas.emails.send exists, is durable, has DLQ configured
Test File: tests/EaaS.Infrastructure.Tests/Messaging/QueueTopologyTests.cs
```

**TC-0.3-04: Should_RouteToDeadLetterQueue_When_ConsumerFailsAfterRetries**
```
Type: Integration
Priority: P0
Preconditions: MassTransit InMemoryTestHarness with SendEmailConsumer
Steps:
  1. Configure consumer to always throw exception
  2. Publish SendEmailMessage
  3. Wait for all retries to exhaust
Expected Result: Message appears in the error queue (_error suffix)
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

**TC-0.3-05: Should_RetryWithExponentialBackoff_When_ConsumerFailsTransiently**
```
Type: Integration
Priority: P1
Preconditions: MassTransit InMemoryTestHarness
Steps:
  1. Configure consumer to fail twice, then succeed on third attempt
  2. Publish SendEmailMessage
  3. Observe retry behavior
Expected Result: Message is retried and eventually consumed successfully
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

---

### US-0.4: Health Check Endpoint

> Focus: Verify health check returns correct status and component details.

---

**TC-0.4-01: Should_Return200_When_AllComponentsHealthy**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory with all Testcontainers running
Steps:
  1. Send GET /health
Expected Result: 200 OK; status = "healthy"; all components report healthy
Test File: tests/EaaS.Integration.Tests/Features/Health/HealthCheckTests.cs
```

**TC-0.4-02: Should_Return503_When_DatabaseDown**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory with PostgreSQL container stopped/unreachable
Steps:
  1. Stop PostgreSQL Testcontainer or provide bad connection string
  2. Send GET /health
Expected Result: 503; database component reports unhealthy
Test File: tests/EaaS.Integration.Tests/Features/Health/HealthCheckTests.cs
```

**TC-0.4-03: Should_Return503_When_RedisDown**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory with Redis container stopped
Steps:
  1. Stop Redis Testcontainer
  2. Send GET /health
Expected Result: 503; redis component reports unhealthy
Test File: tests/EaaS.Integration.Tests/Features/Health/HealthCheckTests.cs
```

**TC-0.4-04: Should_Return503_When_RabbitMqDown**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory with RabbitMQ container stopped
Steps:
  1. Stop RabbitMQ Testcontainer
  2. Send GET /health
Expected Result: 503; rabbitmq component reports unhealthy
Test File: tests/EaaS.Integration.Tests/Features/Health/HealthCheckTests.cs
```

**TC-0.4-05: Should_IncludeLatency_When_ComponentHealthy**
```
Type: Integration
Priority: P1
Preconditions: All containers running
Steps:
  1. Send GET /health
  2. Parse response components
Expected Result: Each component includes latency_ms >= 0
Test File: tests/EaaS.Integration.Tests/Features/Health/HealthCheckTests.cs
```

**TC-0.4-06: Should_NotRequireAuthentication_When_AccessingHealth**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. Send GET /health with NO Authorization header
Expected Result: 200 OK (not 401)
Test File: tests/EaaS.Integration.Tests/Features/Health/HealthCheckTests.cs
```

**TC-0.4-07: Should_ReturnVersionAndUptime_When_Healthy**
```
Type: Integration
Priority: P2
Preconditions: WebApplicationFactory running
Steps:
  1. Send GET /health
  2. Parse response
Expected Result: Response includes version string and uptime_seconds >= 0
Test File: tests/EaaS.Integration.Tests/Features/Health/HealthCheckTests.cs
```

---

### US-0.5: Structured Logging with Serilog

> Focus: Verify Serilog outputs structured JSON, correlation IDs propagate, and sensitive data is excluded.

---

**TC-0.5-01: Should_OutputJsonLogs_When_RequestProcessed**
```
Type: Integration
Priority: P0
Preconditions: Serilog configured with TestSink or InMemorySink
Steps:
  1. Send any API request
  2. Capture log output
Expected Result: Log output is valid JSON with expected properties (Timestamp, Level, MessageTemplate, etc.)
Test File: tests/EaaS.Integration.Tests/Infrastructure/LoggingTests.cs
```

**TC-0.5-02: Should_PropagateCorrelationId_When_XRequestIdProvided**
```
Type: Integration
Priority: P0
Preconditions: API running with logging middleware
Steps:
  1. Send GET /health with X-Request-Id: "test-correlation-123"
  2. Capture log entries
Expected Result: All log entries for this request contain CorrelationId = "test-correlation-123"
Test File: tests/EaaS.Integration.Tests/Infrastructure/LoggingTests.cs
```

**TC-0.5-03: Should_GenerateCorrelationId_When_XRequestIdMissing**
```
Type: Integration
Priority: P1
Preconditions: API running
Steps:
  1. Send GET /health WITHOUT X-Request-Id header
  2. Capture log entries
Expected Result: Log entries contain a generated CorrelationId (non-null, non-empty)
Test File: tests/EaaS.Integration.Tests/Infrastructure/LoggingTests.cs
```

**TC-0.5-04: Should_ExcludeApiKeyFromLogs_When_AuthHeaderPresent**
```
Type: Integration
Priority: P0
Preconditions: API running with logging middleware
Steps:
  1. Send request with Authorization: Bearer eaas_live_secretkey123
  2. Capture all log entries
Expected Result: No log entry contains the full API key value
Test File: tests/EaaS.Integration.Tests/Infrastructure/LoggingTests.cs
```

**TC-0.5-05: Should_LogRequestDuration_When_RequestCompleted**
```
Type: Integration
Priority: P1
Preconditions: API running with request logging middleware
Steps:
  1. Send GET /health
  2. Capture log entries
Expected Result: Log entry contains Elapsed/Duration property > 0
Test File: tests/EaaS.Integration.Tests/Infrastructure/LoggingTests.cs
```

---

### US-0.6: Environment Configuration Management

> Focus: Verify IOptions<T> binding, startup validation, and fail-fast behavior.

---

**TC-0.6-01: Should_BindDatabaseOptions_When_EnvironmentVariablesSet**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create IConfiguration from in-memory dictionary with database settings
  2. Bind to DatabaseOptions
Expected Result: All properties populated correctly
Test File: tests/EaaS.Api.Tests/Configuration/OptionsBindingTests.cs
```

**TC-0.6-02: Should_BindRedisOptions_When_EnvironmentVariablesSet**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create IConfiguration with Redis settings
  2. Bind to RedisOptions
Expected Result: All properties populated correctly
Test File: tests/EaaS.Api.Tests/Configuration/OptionsBindingTests.cs
```

**TC-0.6-03: Should_BindRabbitMqOptions_When_EnvironmentVariablesSet**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create IConfiguration with RabbitMQ settings
  2. Bind to RabbitMqOptions
Expected Result: All properties populated correctly
Test File: tests/EaaS.Api.Tests/Configuration/OptionsBindingTests.cs
```

**TC-0.6-04: Should_BindSesOptions_When_EnvironmentVariablesSet**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create IConfiguration with SES settings
  2. Bind to SesOptions
Expected Result: All properties populated correctly
Test File: tests/EaaS.Api.Tests/Configuration/OptionsBindingTests.cs
```

**TC-0.6-05: Should_FailFast_When_RequiredDatabaseSettingMissing**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create IConfiguration WITHOUT database connection string
  2. Attempt to validate options on startup
Expected Result: OptionsValidationException thrown with clear message about missing field
Test File: tests/EaaS.Api.Tests/Configuration/OptionsValidationTests.cs
```

**TC-0.6-06: Should_FailFast_When_RequiredRedisSettingMissing**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create IConfiguration WITHOUT Redis connection string
  2. Attempt to validate options
Expected Result: OptionsValidationException thrown
Test File: tests/EaaS.Api.Tests/Configuration/OptionsValidationTests.cs
```

**TC-0.6-07: Should_FailFast_When_RequiredSesSettingMissing**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create IConfiguration WITHOUT SES region or credentials
  2. Attempt to validate options
Expected Result: OptionsValidationException thrown
Test File: tests/EaaS.Api.Tests/Configuration/OptionsValidationTests.cs
```

---

### US-5.1: Create an API Key

> Focus: Verify key generation, hashing, storage, and response contract.

---

**TC-5.1-01: Should_Return201WithApiKey_When_ValidRequestSent**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running, authenticated with bootstrap key
Steps:
  1. POST /api/v1/keys with { "name": "Test App" }
Expected Result: 201 Created; response contains key_id, name, api_key (full key), prefix, created_at
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-02: Should_ReturnKeyWithCorrectFormat_When_Created**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/keys with { "name": "Test App" }
  2. Extract api_key from response
Expected Result: Key starts with "eaas_live_" and total length is 49 (9 prefix + 40 random)
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-03: Should_StoreSha256Hash_When_KeyCreated**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory with DB access
Steps:
  1. POST /api/v1/keys
  2. Extract api_key from response
  3. Compute SHA-256 hash of the key
  4. Query api_keys table for the hash
Expected Result: Record found with matching key_hash; plaintext key NOT stored anywhere in DB
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-04: Should_StorePrefix_When_KeyCreated**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory with DB access
Steps:
  1. POST /api/v1/keys
  2. Extract api_key and prefix from response
Expected Result: prefix equals first 8 characters of the api_key
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-05: Should_BeImmediatelyActive_When_KeyCreated**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/keys to create a new key
  2. Use the new key in Authorization header
  3. Send GET /health (or any authenticated endpoint)
Expected Result: Request succeeds (not 401)
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-06: Should_AcceptAllowedDomains_When_Provided**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/keys with { "name": "Test", "allowed_domains": ["example.com"] }
Expected Result: Response includes allowed_domains: ["example.com"]
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-07: Should_Return400_When_NameMissing**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/keys with {}
Expected Result: 400; error code VALIDATION_ERROR; details mention "name" field
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-08: Should_Return401_When_NoAuthHeader**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/keys WITHOUT Authorization header
Expected Result: 401 Unauthorized
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/CreateApiKeyTests.cs
```

**TC-5.1-09: Should_GenerateCryptographicallySecureKey_When_Created**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Generate 100 API keys
  2. Check uniqueness
Expected Result: All 100 keys are unique; key uses alphanumeric characters only
Test File: tests/EaaS.Api.Tests/Features/ApiKeys/ApiKeyGeneratorTests.cs
```

**TC-5.1-10: Should_HashKeyWithSha256_When_Storing**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Hash a known key "eaas_live_test1234567890abcdefghijklmnopqrst"
  2. Compare with expected SHA-256 hex digest
Expected Result: Hash matches expected SHA-256 output; hash is 64 characters hex
Test File: tests/EaaS.Api.Tests/Features/ApiKeys/ApiKeyHashTests.cs
```

#### Validator Tests

**TC-5.1-11: Should_PassValidation_When_NameProvided**
```
Type: Unit
Priority: P0
Preconditions: CreateApiKeyValidator instantiated
Steps:
  1. Validate { "name": "CashTrack Production" }
Expected Result: Validation passes
Test File: tests/EaaS.Api.Tests/Features/ApiKeys/CreateApiKeyValidatorTests.cs
```

**TC-5.1-12: Should_FailValidation_When_NameEmpty**
```
Type: Unit
Priority: P0
Preconditions: CreateApiKeyValidator instantiated
Steps:
  1. Validate { "name": "" }
Expected Result: Validation fails with error on "name" field
Test File: tests/EaaS.Api.Tests/Features/ApiKeys/CreateApiKeyValidatorTests.cs
```

**TC-5.1-13: Should_FailValidation_When_NameExceeds255Chars**
```
Type: Unit
Priority: P1
Preconditions: CreateApiKeyValidator instantiated
Steps:
  1. Validate { "name": "a" * 256 }
Expected Result: Validation fails with error on "name" field
Test File: tests/EaaS.Api.Tests/Features/ApiKeys/CreateApiKeyValidatorTests.cs
```

---

### US-5.3: Revoke an API Key

> Focus: Verify revocation is immediate, returns 401 on subsequent use, and queued emails continue.

---

**TC-5.3-01: Should_Return204_When_ActiveKeyRevoked**
```
Type: Integration
Priority: P0
Preconditions: An active API key exists
Steps:
  1. DELETE /api/v1/keys/{key_id}
Expected Result: 204 No Content
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/RevokeApiKeyTests.cs
```

**TC-5.3-02: Should_Return401_When_RevokedKeyUsed**
```
Type: Integration
Priority: P0
Preconditions: API key created and then revoked
Steps:
  1. Create API key
  2. Revoke it via DELETE /api/v1/keys/{key_id}
  3. Use the revoked key in Authorization header for any request
Expected Result: 401 Unauthorized
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/RevokeApiKeyTests.cs
```

**TC-5.3-03: Should_SetRevokedAtTimestamp_When_KeyRevoked**
```
Type: Integration
Priority: P1
Preconditions: API key exists in DB
Steps:
  1. Revoke key
  2. Query api_keys table
Expected Result: revoked_at is not null and approximately current time; status = 'revoked'
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/RevokeApiKeyTests.cs
```

**TC-5.3-04: Should_Return404_When_RevokingNonexistentKey**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory running
Steps:
  1. DELETE /api/v1/keys/{random-uuid}
Expected Result: 404 Not Found
Test File: tests/EaaS.Integration.Tests/Features/ApiKeys/RevokeApiKeyTests.cs
```

**TC-5.3-05: Should_ContinueProcessingQueuedEmails_When_KeyRevoked**
```
Type: E2E
Priority: P0
Preconditions: Email already enqueued with this API key
Steps:
  1. Send email (gets queued)
  2. Immediately revoke the API key
  3. Let worker process the queued message
Expected Result: Email is processed and sent successfully despite key revocation
Test File: tests/EaaS.Integration.Tests/Scenarios/RevokedKeyQueuedEmailTests.cs
```

#### Authentication Middleware Tests

**TC-5.3-06: Should_Return401_When_AuthHeaderMissing**
```
Type: Unit
Priority: P0
Preconditions: ApiKeyAuthenticationHandler instantiated with mock IApiKeyRepository
Steps:
  1. Create HTTP context with no Authorization header
  2. Invoke handler
Expected Result: AuthenticateResult.Fail; 401 response
Test File: tests/EaaS.Api.Tests/Middleware/ApiKeyAuthenticationHandlerTests.cs
```

**TC-5.3-07: Should_Return401_When_AuthHeaderInvalidFormat**
```
Type: Unit
Priority: P0
Preconditions: ApiKeyAuthenticationHandler instantiated
Steps:
  1. Create HTTP context with Authorization: "InvalidFormat"
  2. Invoke handler
Expected Result: AuthenticateResult.Fail
Test File: tests/EaaS.Api.Tests/Middleware/ApiKeyAuthenticationHandlerTests.cs
```

**TC-5.3-08: Should_Return401_When_KeyNotFoundInDatabase**
```
Type: Unit
Priority: P0
Preconditions: ApiKeyAuthenticationHandler with mock repository returning null
Steps:
  1. Create HTTP context with Authorization: "Bearer eaas_live_unknownkey"
  2. Invoke handler
Expected Result: AuthenticateResult.Fail
Test File: tests/EaaS.Api.Tests/Middleware/ApiKeyAuthenticationHandlerTests.cs
```

**TC-5.3-09: Should_Authenticate_When_ValidActiveKeyProvided**
```
Type: Unit
Priority: P0
Preconditions: ApiKeyAuthenticationHandler with mock repository returning active key
Steps:
  1. Create HTTP context with valid Bearer token
  2. Invoke handler
Expected Result: AuthenticateResult.Success with claims including TenantId and ApiKeyId
Test File: tests/EaaS.Api.Tests/Middleware/ApiKeyAuthenticationHandlerTests.cs
```

**TC-5.3-10: Should_UpdateLastUsedAt_When_KeyAuthenticated**
```
Type: Unit
Priority: P1
Preconditions: ApiKeyAuthenticationHandler with mock repository
Steps:
  1. Authenticate with valid key
  2. Verify repository update called
Expected Result: last_used_at updated to current time
Test File: tests/EaaS.Api.Tests/Middleware/ApiKeyAuthenticationHandlerTests.cs
```

#### Rate Limiting Tests

**TC-5.3-11: Should_Return429_When_RateLimitExceeded**
```
Type: Unit
Priority: P0
Preconditions: RateLimitingMiddleware with mock Redis returning count > 100
Steps:
  1. Simulate 101st request within the sliding window
Expected Result: 429 Too Many Requests; error code RATE_LIMITED
Test File: tests/EaaS.Api.Tests/Middleware/RateLimitingMiddlewareTests.cs
```

**TC-5.3-12: Should_AllowRequest_When_UnderRateLimit**
```
Type: Unit
Priority: P0
Preconditions: RateLimitingMiddleware with mock Redis returning count < 100
Steps:
  1. Simulate request when count is below limit
Expected Result: Request passes through to next middleware
Test File: tests/EaaS.Api.Tests/Middleware/RateLimitingMiddlewareTests.cs
```

---

### US-3.1: Add a Sending Domain

> Focus: Verify domain creation, SES integration, DNS record generation, and duplicate handling.

---

**TC-3.1-01: Should_Return201WithDnsRecords_When_ValidDomainAdded**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory with FakeSesService, authenticated
Steps:
  1. POST /api/v1/domains with { "domain": "test.example.com" }
Expected Result: 201 Created; response contains domain_id, domain, status="pending_verification", dns_records array with 5 records (1 SPF + 3 DKIM + 1 DMARC), created_at
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-02: Should_GenerateSpfRecord_When_DomainAdded**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/domains with { "domain": "test.example.com" }
  2. Extract dns_records from response
Expected Result: One TXT record with purpose "spf" containing "v=spf1 include:amazonses.com ~all"
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-03: Should_GenerateThreeDkimRecords_When_DomainAdded**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/domains with { "domain": "test.example.com" }
  2. Extract dns_records with purpose "dkim"
Expected Result: Exactly 3 CNAME records with purpose "dkim", each pointing to *.dkim.amazonses.com
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-04: Should_GenerateDmarcRecord_When_DomainAdded**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/domains with { "domain": "test.example.com" }
  2. Extract dns_records with purpose "dmarc"
Expected Result: One TXT record with name "_dmarc.test.example.com" containing "v=DMARC1"
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-05: Should_SetStatusToPendingVerification_When_DomainAdded**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/domains
  2. Check response status field
Expected Result: status = "pending_verification"
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-06: Should_Return409_When_DuplicateDomainAdded**
```
Type: Integration
Priority: P0
Preconditions: Domain "test.example.com" already exists
Steps:
  1. POST /api/v1/domains with { "domain": "test.example.com" }
Expected Result: 409 Conflict; error code CONFLICT
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-07: Should_Return400_When_InvalidDomainFormat**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/domains with { "domain": "not a domain" }
Expected Result: 400; error code VALIDATION_ERROR
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-08: Should_Return400_When_DomainMissing**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/domains with {}
Expected Result: 400; VALIDATION_ERROR with detail about missing domain field
Test File: tests/EaaS.Integration.Tests/Features/Domains/AddDomainTests.cs
```

**TC-3.1-09: Should_CallSesVerifyDomainIdentity_When_DomainAdded**
```
Type: Unit
Priority: P0
Preconditions: AddDomainCommandHandler with mock IEmailDeliveryService/SES service
Steps:
  1. Execute AddDomainCommand
  2. Verify SES VerifyDomainIdentity was called with correct domain
Expected Result: SES API called once with the domain name
Test File: tests/EaaS.Api.Tests/Features/Domains/AddDomainCommandHandlerTests.cs
```

**TC-3.1-10: Should_PersistDomainAndDnsRecords_When_DomainAdded**
```
Type: Unit
Priority: P0
Preconditions: AddDomainCommandHandler with mock repositories
Steps:
  1. Execute AddDomainCommand
  2. Verify domain repository Save called
  3. Verify DNS records saved
Expected Result: Domain entity saved with correct tenant_id; 5 DNS records saved
Test File: tests/EaaS.Api.Tests/Features/Domains/AddDomainCommandHandlerTests.cs
```

#### Validator Tests

**TC-3.1-11: Should_PassValidation_When_ValidDomainProvided**
```
Type: Unit
Priority: P0
Preconditions: AddDomainValidator instantiated
Steps:
  1. Validate { "domain": "notifications.example.com" }
Expected Result: Validation passes
Test File: tests/EaaS.Api.Tests/Features/Domains/AddDomainValidatorTests.cs
```

**TC-3.1-12: Should_FailValidation_When_DomainEmpty**
```
Type: Unit
Priority: P0
Preconditions: AddDomainValidator instantiated
Steps:
  1. Validate { "domain": "" }
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Domains/AddDomainValidatorTests.cs
```

**TC-3.1-13: Should_FailValidation_When_DomainHasInvalidCharacters**
```
Type: Unit
Priority: P1
Preconditions: AddDomainValidator instantiated
Steps:
  1. Validate { "domain": "test domain!@#.com" }
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Domains/AddDomainValidatorTests.cs
```

---

### US-3.2: Verify Domain DNS Configuration

> Focus: Verify DNS checking logic, per-record verification, and status transitions.

---

**TC-3.2-01: Should_ReturnVerified_When_AllDnsRecordsMatch**
```
Type: Integration
Priority: P0
Preconditions: Domain exists with pending status; FakeDnsVerifier configured to return all passing
Steps:
  1. POST /api/v1/domains/{id}/verify
Expected Result: 200 OK; domain status = "verified"; all dns_records[].is_verified = true; verified_at set
Test File: tests/EaaS.Integration.Tests/Features/Domains/VerifyDomainTests.cs
```

**TC-3.2-02: Should_ReturnFailed_When_SpfRecordMissing**
```
Type: Integration
Priority: P0
Preconditions: FakeDnsVerifier configured with SPF failing, DKIM/DMARC passing
Steps:
  1. POST /api/v1/domains/{id}/verify
Expected Result: 200 OK; domain status = "failed"; SPF record is_verified = false; DKIM/DMARC records is_verified = true
Test File: tests/EaaS.Integration.Tests/Features/Domains/VerifyDomainTests.cs
```

**TC-3.2-03: Should_ReturnFailed_When_DkimRecordsMissing**
```
Type: Integration
Priority: P0
Preconditions: FakeDnsVerifier configured with DKIM failing
Steps:
  1. POST /api/v1/domains/{id}/verify
Expected Result: domain status = "failed"; DKIM records show is_verified = false
Test File: tests/EaaS.Integration.Tests/Features/Domains/VerifyDomainTests.cs
```

**TC-3.2-04: Should_ReturnFailed_When_DmarcRecordMissing**
```
Type: Integration
Priority: P1
Preconditions: FakeDnsVerifier configured with DMARC failing
Steps:
  1. POST /api/v1/domains/{id}/verify
Expected Result: domain status = "failed"; DMARC record is_verified = false
Test File: tests/EaaS.Integration.Tests/Features/Domains/VerifyDomainTests.cs
```

**TC-3.2-05: Should_IncludeExpectedAndActualValues_When_RecordFails**
```
Type: Integration
Priority: P1
Preconditions: FakeDnsVerifier returns mismatched value for SPF
Steps:
  1. POST /api/v1/domains/{id}/verify
Expected Result: SPF record includes "expected" and "actual" fields showing the mismatch
Test File: tests/EaaS.Integration.Tests/Features/Domains/VerifyDomainTests.cs
```

**TC-3.2-06: Should_Return404_When_DomainIdNotFound**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/domains/{random-uuid}/verify
Expected Result: 404 Not Found
Test File: tests/EaaS.Integration.Tests/Features/Domains/VerifyDomainTests.cs
```

**TC-3.2-07: Should_CheckEachRecordIndependently_When_Verifying**
```
Type: Unit
Priority: P0
Preconditions: VerifyDomainCommandHandler with mock IDnsVerifier
Steps:
  1. Configure mock to return pass for SPF, fail for first DKIM, pass for remaining
  2. Execute command
Expected Result: Each record checked independently; partial verification results persisted
Test File: tests/EaaS.Api.Tests/Features/Domains/VerifyDomainCommandHandlerTests.cs
```

**TC-3.2-08: Should_UpdateVerifiedAtTimestamp_When_AllRecordsPass**
```
Type: Unit
Priority: P1
Preconditions: VerifyDomainCommandHandler with mock repos
Steps:
  1. Configure all DNS checks to pass
  2. Execute command
Expected Result: Domain entity verified_at and last_checked_at set to current time
Test File: tests/EaaS.Api.Tests/Features/Domains/VerifyDomainCommandHandlerTests.cs
```

---

### US-1.1: Send a Single Email (End-to-End)

> Focus: The most critical story. Tests cover validation, auth, suppression check, domain verification, queuing, worker processing, SES delivery, and status tracking.

---

#### API Endpoint Tests

**TC-1.1-01: Should_Return202WithMessageId_When_ValidEmailSent**
```
Type: Integration
Priority: P0
Preconditions: Authenticated, verified domain exists, recipient not suppressed
Steps:
  1. POST /api/v1/emails/send with valid from/to/subject/html_body
Expected Result: 202 Accepted; response contains message_id (format: msg_ + 12 chars), status = "queued", queued_at timestamp
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-02: Should_Return400_When_ToFieldMissing**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/emails/send without "to" field
Expected Result: 400; VALIDATION_ERROR; details include "to" field error
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-03: Should_Return400_When_FromFieldMissing**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/emails/send without "from" field
Expected Result: 400; VALIDATION_ERROR; details include "from" field error
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-04: Should_Return400_When_SubjectMissing_AndNoTemplateId**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/emails/send with from, to, html_body but NO subject and NO template_id
Expected Result: 400; VALIDATION_ERROR
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-05: Should_Return400_When_HtmlBodyMissing_AndNoTemplateId**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/emails/send with from, to, subject but NO html_body and NO template_id
Expected Result: 400; VALIDATION_ERROR
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-06: Should_Return400_When_ToEmailInvalidFormat**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/emails/send with to: [{ "email": "not-an-email" }]
Expected Result: 400; VALIDATION_ERROR
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-07: Should_Return400_When_FromEmailInvalidFormat**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/emails/send with from: { "email": "bad-email" }
Expected Result: 400; VALIDATION_ERROR
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-08: Should_Return401_When_NoAuthorizationHeader**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/emails/send with valid body but NO Authorization header
Expected Result: 401 Unauthorized; UNAUTHORIZED error code
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-09: Should_Return401_When_InvalidApiKey**
```
Type: Integration
Priority: P0
Preconditions: WebApplicationFactory running
Steps:
  1. POST /api/v1/emails/send with Authorization: Bearer eaas_live_invalidkey
Expected Result: 401 Unauthorized
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-10: Should_Return422_When_RecipientSuppressed**
```
Type: Integration
Priority: P0
Preconditions: Recipient "suppressed@example.com" in suppression list
Steps:
  1. POST /api/v1/emails/send with to: [{ "email": "suppressed@example.com" }]
Expected Result: 422; RECIPIENT_SUPPRESSED error code; message identifies suppressed address
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-11: Should_Return422_When_FromDomainNotVerified**
```
Type: Integration
Priority: P0
Preconditions: Domain "unverified.com" exists with status "pending_verification"
Steps:
  1. POST /api/v1/emails/send with from: { "email": "noreply@unverified.com" }
Expected Result: 422; DOMAIN_NOT_VERIFIED error code
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-12: Should_Return422_When_FromDomainNotRegistered**
```
Type: Integration
Priority: P0
Preconditions: No domain registered for "unknown.com"
Steps:
  1. POST /api/v1/emails/send with from: { "email": "noreply@unknown.com" }
Expected Result: 422; DOMAIN_NOT_VERIFIED (or 400)
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-13: Should_Return403_When_ApiKeyNotAllowedForDomain**
```
Type: Integration
Priority: P1
Preconditions: API key with allowed_domains: ["app1.com"]; verified domain "app2.com" exists
Steps:
  1. POST /api/v1/emails/send with from: { "email": "noreply@app2.com" } using restricted key
Expected Result: 403; FORBIDDEN
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-14: Should_Return429_When_RateLimited**
```
Type: Integration
Priority: P0
Preconditions: Rate limit set to a low value (e.g., 5 for testing)
Steps:
  1. Send 6 rapid POST /api/v1/emails/send requests
Expected Result: First 5 return 202; 6th returns 429
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-15: Should_AcceptTagsAndMetadata_When_Provided**
```
Type: Integration
Priority: P1
Preconditions: Valid email request
Steps:
  1. POST /api/v1/emails/send with tags: ["invoice"] and metadata: { "invoice_id": "123" }
Expected Result: 202 Accepted; tags and metadata stored (verifiable via GET /api/v1/emails/{id})
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-16: Should_AcceptOptionalTextBody_When_Provided**
```
Type: Integration
Priority: P1
Preconditions: Valid email request
Steps:
  1. POST /api/v1/emails/send with html_body AND text_body
Expected Result: 202 Accepted; both bodies stored
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendEmailTests.cs
```

**TC-1.1-17: Should_Return400_When_ToExceeds50Recipients**
```
Type: Unit
Priority: P1
Preconditions: SendEmailValidator instantiated
Steps:
  1. Validate request with 51 recipients in "to" array
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

#### Handler Tests (Unit)

**TC-1.1-18: Should_PublishToQueue_When_ValidationPasses**
```
Type: Unit
Priority: P0
Preconditions: SendEmailCommandHandler with mock IMessagePublisher
Steps:
  1. Execute SendEmailCommand with valid data
  2. Verify IMessagePublisher.PublishAsync called
Expected Result: SendEmailMessage published to queue with correct email_id and message_id
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailCommandHandlerTests.cs
```

**TC-1.1-19: Should_SaveEmailToDatabase_When_Queued**
```
Type: Unit
Priority: P0
Preconditions: SendEmailCommandHandler with mock IEmailRepository
Steps:
  1. Execute SendEmailCommand
  2. Verify IEmailRepository.AddAsync called
Expected Result: Email entity saved with status = Queued, correct tenant_id, api_key_id
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailCommandHandlerTests.cs
```

**TC-1.1-20: Should_CheckSuppressionList_When_Sending**
```
Type: Unit
Priority: P0
Preconditions: SendEmailCommandHandler with mock ISuppressionCache
Steps:
  1. Configure mock to return true for "suppressed@test.com"
  2. Execute command with that recipient
Expected Result: RecipientSuppressedException thrown (or handler returns 422 result)
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailCommandHandlerTests.cs
```

**TC-1.1-21: Should_VerifyFromDomain_When_Sending**
```
Type: Unit
Priority: P0
Preconditions: SendEmailCommandHandler with mock IDomainRepository
Steps:
  1. Configure mock to return domain with status != Verified
  2. Execute command
Expected Result: DomainNotVerifiedException thrown
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailCommandHandlerTests.cs
```

**TC-1.1-22: Should_GenerateUniqueMessageId_When_EmailCreated**
```
Type: Unit
Priority: P0
Preconditions: IdGenerator class
Steps:
  1. Call IdGenerator.NewMessageId() 100 times
Expected Result: All IDs unique; all match pattern msg_[a-zA-Z0-9]{12}
Test File: tests/EaaS.Api.Tests/Helpers/IdGeneratorTests.cs
```

#### GET /api/v1/emails/{message_id} Tests

**TC-1.1-23: Should_ReturnEmailDetails_When_ValidMessageId**
```
Type: Integration
Priority: P0
Preconditions: Email exists in database
Steps:
  1. GET /api/v1/emails/{message_id}
Expected Result: 200 OK; response matches email detail contract (message_id, from, to, subject, status, events[])
Test File: tests/EaaS.Integration.Tests/Features/Emails/GetEmailTests.cs
```

**TC-1.1-24: Should_Return404_When_MessageIdNotFound**
```
Type: Integration
Priority: P0
Preconditions: No email with given ID
Steps:
  1. GET /api/v1/emails/msg_nonexistent
Expected Result: 404 Not Found
Test File: tests/EaaS.Integration.Tests/Features/Emails/GetEmailTests.cs
```

**TC-1.1-25: Should_IncludeEventTimeline_When_EmailHasEvents**
```
Type: Integration
Priority: P1
Preconditions: Email with multiple events (queued, sent, delivered)
Steps:
  1. GET /api/v1/emails/{message_id}
  2. Check events array
Expected Result: Events array contains ordered events with type and timestamp
Test File: tests/EaaS.Integration.Tests/Features/Emails/GetEmailTests.cs
```

#### Worker/Consumer Tests

**TC-1.1-26: Should_SendViaSes_When_MessageConsumed**
```
Type: Unit
Priority: P0
Preconditions: SendEmailConsumer with mock IEmailDeliveryService
Steps:
  1. Consume SendEmailMessage
  2. Verify IEmailDeliveryService.SendAsync called
Expected Result: SES service called with correct from, to, subject, body
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

**TC-1.1-27: Should_UpdateStatusToSent_When_SesSucceeds**
```
Type: Unit
Priority: P0
Preconditions: SendEmailConsumer with mock SES (success) and mock IEmailRepository
Steps:
  1. Consume message
  2. Verify email status updated
Expected Result: Email status updated to Sent; sent_at timestamp set; ses_message_id stored
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

**TC-1.1-28: Should_UpdateStatusToFailed_When_SesFails**
```
Type: Unit
Priority: P0
Preconditions: SendEmailConsumer with mock SES throwing exception
Steps:
  1. Consume message where SES throws
Expected Result: After retries exhausted, email status = Failed; error_message populated
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

**TC-1.1-29: Should_CreateQueuedEvent_When_MessageConsumed**
```
Type: Unit
Priority: P1
Preconditions: SendEmailConsumer with mock repos
Steps:
  1. Consume message
Expected Result: EmailEvent with type "queued" (or "sent") created
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

**TC-1.1-30: Should_PropagateCorrelationId_When_Processing**
```
Type: Unit
Priority: P1
Preconditions: SendEmailConsumer with message containing correlation ID in headers
Steps:
  1. Consume message with X-Request-Id header
  2. Check log output
Expected Result: Worker logs contain the same correlation ID
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

#### Validator Tests (Full Coverage)

**TC-1.1-31: Should_PassValidation_When_AllRequiredFieldsProvided**
```
Type: Unit
Priority: P0
Preconditions: SendEmailValidator instantiated
Steps:
  1. Validate { from: { email: "a@verified.com" }, to: [{ email: "b@test.com" }], subject: "Test", html_body: "<p>Hi</p>" }
Expected Result: Validation passes
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

**TC-1.1-32: Should_FailValidation_When_BothHtmlBodyAndTemplateIdProvided**
```
Type: Unit
Priority: P0
Preconditions: SendEmailValidator instantiated
Steps:
  1. Validate request with html_body AND template_id
Expected Result: Validation fails -- mutually exclusive
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

**TC-1.1-33: Should_FailValidation_When_NeitherHtmlBodyNorTemplateIdProvided**
```
Type: Unit
Priority: P0
Preconditions: SendEmailValidator instantiated
Steps:
  1. Validate request with subject but no html_body and no template_id
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

**TC-1.1-34: Should_FailValidation_When_ToArrayEmpty**
```
Type: Unit
Priority: P0
Preconditions: SendEmailValidator instantiated
Steps:
  1. Validate request with to: []
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

**TC-1.1-35: Should_PassValidation_When_TemplateIdProvidedWithoutSubject**
```
Type: Unit
Priority: P0
Preconditions: SendEmailValidator instantiated
Steps:
  1. Validate request with template_id and variables, but no subject/html_body
Expected Result: Validation passes (template provides the subject and body)
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

**TC-1.1-36: Should_FailValidation_When_CombinedRecipientExceeds50**
```
Type: Unit
Priority: P1
Preconditions: SendEmailValidator instantiated
Steps:
  1. Validate request with to: 20, cc: 20, bcc: 11 (total 51)
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

#### E2E Tests

**TC-1.1-37: Should_DeliverEmailEndToEnd_When_ValidRequestSent**
```
Type: E2E
Priority: P0
Preconditions: Full stack running (WebApplicationFactory + MassTransit + Testcontainers + FakeSES)
Steps:
  1. POST /api/v1/emails/send with valid email
  2. Wait for worker to consume message (poll or use MassTransit test harness)
  3. GET /api/v1/emails/{message_id}
Expected Result: Email status progresses from "queued" to "sent"; FakeSES records the email; events timeline shows queued and sent events
Test File: tests/EaaS.Integration.Tests/Scenarios/SendEmailE2ETests.cs
```

**TC-1.1-38: Should_RejectSuppressedRecipient_EndToEnd**
```
Type: E2E
Priority: P0
Preconditions: Suppression entry for "blocked@test.com" in DB and Redis cache
Steps:
  1. POST /api/v1/emails/send with to: [{ "email": "blocked@test.com" }]
Expected Result: 422 at API level; email never enqueued; FakeSES not called
Test File: tests/EaaS.Integration.Tests/Scenarios/SendEmailE2ETests.cs
```

---

### US-1.3: Send an Email Using a Template

> Focus: Template reference, variable rendering, missing template/variable handling.

---

**TC-1.3-01: Should_Return202_When_ValidTemplateIdAndVariables**
```
Type: Integration
Priority: P0
Preconditions: Template exists with id "tmpl_abc12345"; verified domain; authenticated
Steps:
  1. POST /api/v1/emails/send with template_id: "tmpl_abc12345", variables: { "name": "John" }
Expected Result: 202 Accepted; message_id returned
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendTemplateEmailTests.cs
```

**TC-1.3-02: Should_Return404_When_TemplateIdNotFound**
```
Type: Integration
Priority: P0
Preconditions: No template with id "tmpl_nonexistent"
Steps:
  1. POST /api/v1/emails/send with template_id: "tmpl_nonexistent"
Expected Result: 404; NOT_FOUND
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendTemplateEmailTests.cs
```

**TC-1.3-03: Should_Return400_When_RequiredTemplateVariablesMissing**
```
Type: Integration
Priority: P0
Preconditions: Template with variables_schema requiring "client_name"
Steps:
  1. POST /api/v1/emails/send with template_id but variables: {} (missing client_name)
Expected Result: 400; VALIDATION_ERROR; details list missing variables
Test File: tests/EaaS.Integration.Tests/Features/Emails/SendTemplateEmailTests.cs
```

**TC-1.3-04: Should_RenderTemplateWithVariables_When_WorkerProcesses**
```
Type: Unit
Priority: P0
Preconditions: SendEmailConsumer with real FluidTemplateRenderer
Steps:
  1. Consume message with template_id and variables
  2. Verify rendered HTML passed to SES
Expected Result: Template variables substituted in rendered output
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

**TC-1.3-05: Should_UseTemplatePrecedence_When_BothHtmlBodyAndTemplateIdProvided**
```
Type: Unit
Priority: P1
Preconditions: SendEmailCommandHandler
Steps:
  1. Submit request with both template_id and html_body
Expected Result: Validation rejects the request (mutually exclusive per validator TC-1.1-32)
Test File: tests/EaaS.Api.Tests/Features/Emails/SendEmailValidatorTests.cs
```

**TC-1.3-06: Should_MoveToDeadLetterQueue_When_TemplateRenderingFails**
```
Type: Unit
Priority: P0
Preconditions: SendEmailConsumer with mock ITemplateRenderer that throws TemplateRenderException
Steps:
  1. Consume message with template_id
  2. Template renderer throws
Expected Result: After retries, message moves to DLQ; email status = Failed; error logged
Test File: tests/EaaS.Worker.Tests/Consumers/SendEmailConsumerTests.cs
```

**TC-1.3-07: Should_RenderSubjectFromTemplate_When_TemplateUsed**
```
Type: Unit
Priority: P1
Preconditions: FluidTemplateRenderer
Steps:
  1. Render subject template "Invoice for {{ client_name }}" with { "client_name": "John" }
Expected Result: Rendered subject = "Invoice for John"
Test File: tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs
```

#### Template Renderer Tests

**TC-1.3-08: Should_RenderLiquidVariables_When_ValidTemplate**
```
Type: Unit
Priority: P0
Preconditions: FluidTemplateRenderer instantiated
Steps:
  1. Render template "<p>Hello {{ name }}</p>" with { "name": "World" }
Expected Result: "<p>Hello World</p>"
Test File: tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs
```

**TC-1.3-09: Should_ThrowTemplateRenderException_When_InvalidSyntax**
```
Type: Unit
Priority: P0
Preconditions: FluidTemplateRenderer
Steps:
  1. Render template with invalid Liquid syntax "{{ unclosed"
Expected Result: TemplateRenderException thrown
Test File: tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs
```

**TC-1.3-10: Should_HandleMissingVariableGracefully_When_NoSchemaEnforced**
```
Type: Unit
Priority: P1
Preconditions: FluidTemplateRenderer, template with {{ name }} but no variable provided
Steps:
  1. Render template with empty variables
Expected Result: Variable rendered as empty string (Liquid default behavior)
Test File: tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs
```

**TC-1.3-11: Should_RenderConditionalBlocks_When_LiquidIfUsed**
```
Type: Unit
Priority: P1
Preconditions: FluidTemplateRenderer
Steps:
  1. Render template "{% if premium %}VIP{% endif %}" with { "premium": true }
Expected Result: "VIP"
Test File: tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs
```

**TC-1.3-12: Should_RenderLoops_When_LiquidForUsed**
```
Type: Unit
Priority: P1
Preconditions: FluidTemplateRenderer
Steps:
  1. Render template "{% for item in items %}{{ item }},{% endfor %}" with { "items": ["a","b","c"] }
Expected Result: "a,b,c,"
Test File: tests/EaaS.Infrastructure.Tests/Templating/FluidTemplateRendererTests.cs
```

---

### US-2.1: Create an Email Template

> Focus: Template CRUD creation, validation, Liquid syntax check, Redis caching.

---

**TC-2.1-01: Should_Return201WithTemplateId_When_ValidTemplateCreated**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/templates with { name: "welcome_email", subject_template: "Welcome {{ name }}", html_body: "<h1>Welcome {{ name }}</h1>" }
Expected Result: 201 Created; template_id matches pattern tmpl_[a-zA-Z0-9]{8}; version = 1; created_at set
Test File: tests/EaaS.Integration.Tests/Features/Templates/CreateTemplateTests.cs
```

**TC-2.1-02: Should_Return409_When_DuplicateNameUsed**
```
Type: Integration
Priority: P0
Preconditions: Template "welcome_email" already exists
Steps:
  1. POST /api/v1/templates with { name: "welcome_email", ... }
Expected Result: 409 Conflict
Test File: tests/EaaS.Integration.Tests/Features/Templates/CreateTemplateTests.cs
```

**TC-2.1-03: Should_Return400_When_LiquidSyntaxInvalid**
```
Type: Integration
Priority: P0
Preconditions: Authenticated
Steps:
  1. POST /api/v1/templates with html_body: "{{ unclosed tag"
Expected Result: 400; error mentions Liquid syntax error
Test File: tests/EaaS.Integration.Tests/Features/Templates/CreateTemplateTests.cs
```

**TC-2.1-04: Should_Return400_When_NameMissing**
```
Type: Unit
Priority: P0
Preconditions: CreateTemplateValidator instantiated
Steps:
  1. Validate request with empty name
Expected Result: Validation fails on name field
Test File: tests/EaaS.Api.Tests/Features/Templates/CreateTemplateValidatorTests.cs
```

**TC-2.1-05: Should_Return400_When_SubjectTemplateMissing**
```
Type: Unit
Priority: P0
Preconditions: CreateTemplateValidator instantiated
Steps:
  1. Validate request without subject_template
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Templates/CreateTemplateValidatorTests.cs
```

**TC-2.1-06: Should_Return400_When_HtmlBodyMissing**
```
Type: Unit
Priority: P0
Preconditions: CreateTemplateValidator instantiated
Steps:
  1. Validate request without html_body
Expected Result: Validation fails
Test File: tests/EaaS.Api.Tests/Features/Templates/CreateTemplateValidatorTests.cs
```

**TC-2.1-07: Should_Return400_When_TemplateSizeExceeds512KB**
```
Type: Unit
Priority: P0
Preconditions: CreateTemplateValidator instantiated
Steps:
  1. Validate request with html_body of 600KB
Expected Result: Validation fails with size limit error
Test File: tests/EaaS.Api.Tests/Features/Templates/CreateTemplateValidatorTests.cs
```

**TC-2.1-08: Should_CacheTemplateInRedis_When_Created**
```
Type: Integration
Priority: P1
Preconditions: WebApplicationFactory with Redis Testcontainer
Steps:
  1. POST /api/v1/templates
  2. Check Redis for template:{id} key
Expected Result: Template body cached in Redis
Test File: tests/EaaS.Integration.Tests/Features/Templates/CreateTemplateTests.cs
```

**TC-2.1-09: Should_ValidateLiquidSyntaxOnSubjectTemplate_When_Creating**
```
Type: Unit
Priority: P1
Preconditions: CreateTemplateCommandHandler
Steps:
  1. Create template with subject_template containing invalid Liquid
Expected Result: Validation error returned
Test File: tests/EaaS.Api.Tests/Features/Templates/CreateTemplateCommandHandlerTests.cs
```

**TC-2.1-10: Should_AcceptOptionalTextBody_When_Provided**
```
Type: Integration
Priority: P1
Preconditions: Authenticated
Steps:
  1. POST /api/v1/templates with text_body included
Expected Result: 201; text_body stored and retrievable
Test File: tests/EaaS.Integration.Tests/Features/Templates/CreateTemplateTests.cs
```

**TC-2.1-11: Should_AcceptVariablesSchema_When_Provided**
```
Type: Integration
Priority: P1
Preconditions: Authenticated
Steps:
  1. POST /api/v1/templates with variables_schema: { "type": "object", "required": ["name"], "properties": { "name": { "type": "string" } } }
Expected Result: 201; variables_schema stored and retrievable via GET
Test File: tests/EaaS.Integration.Tests/Features/Templates/CreateTemplateTests.cs
```

**TC-2.1-12: Should_SetVersionTo1_When_TemplateCreated**
```
Type: Integration
Priority: P1
Preconditions: Authenticated
Steps:
  1. POST /api/v1/templates
  2. GET /api/v1/templates/{id}
Expected Result: version = 1
Test File: tests/EaaS.Integration.Tests/Features/Templates/CreateTemplateTests.cs
```

---

### US-2.2: Update an Email Template

> Focus: Update fields, version increment, cache invalidation, old sends unaffected.

---

**TC-2.2-01: Should_Return200WithUpdatedTemplate_When_ValidUpdate**
```
Type: Integration
Priority: P0
Preconditions: Template exists with version 1
Steps:
  1. PUT /api/v1/templates/{id} with { html_body: "<p>Updated</p>" }
Expected Result: 200 OK; version = 2; updated_at > created_at; html_body is new value
Test File: tests/EaaS.Integration.Tests/Features/Templates/UpdateTemplateTests.cs
```

**TC-2.2-02: Should_IncrementVersion_When_TemplateUpdated**
```
Type: Integration
Priority: P0
Preconditions: Template at version 1
Steps:
  1. PUT /api/v1/templates/{id}
  2. PUT /api/v1/templates/{id} again
Expected Result: Version incremented to 2, then 3
Test File: tests/EaaS.Integration.Tests/Features/Templates/UpdateTemplateTests.cs
```

**TC-2.2-03: Should_InvalidateRedisCache_When_TemplateUpdated**
```
Type: Integration
Priority: P0
Preconditions: Template cached in Redis
Steps:
  1. PUT /api/v1/templates/{id}
  2. Check Redis for old cache entry
Expected Result: Old cache entry removed or replaced
Test File: tests/EaaS.Integration.Tests/Features/Templates/UpdateTemplateTests.cs
```

**TC-2.2-04: Should_Return404_When_UpdatingNonexistentTemplate**
```
Type: Integration
Priority: P0
Preconditions: No template with given ID
Steps:
  1. PUT /api/v1/templates/{random-id}
Expected Result: 404 Not Found
Test File: tests/EaaS.Integration.Tests/Features/Templates/UpdateTemplateTests.cs
```

**TC-2.2-05: Should_Return400_When_UpdatedLiquidSyntaxInvalid**
```
Type: Integration
Priority: P1
Preconditions: Template exists
Steps:
  1. PUT /api/v1/templates/{id} with html_body: "{{ broken"
Expected Result: 400; Liquid syntax error
Test File: tests/EaaS.Integration.Tests/Features/Templates/UpdateTemplateTests.cs
```

**TC-2.2-06: Should_OnlyUpdateProvidedFields_When_PartialUpdate**
```
Type: Integration
Priority: P1
Preconditions: Template exists with name, subject_template, html_body
Steps:
  1. PUT /api/v1/templates/{id} with only { html_body: "<p>New</p>" }
  2. GET /api/v1/templates/{id}
Expected Result: name and subject_template unchanged; html_body updated
Test File: tests/EaaS.Integration.Tests/Features/Templates/UpdateTemplateTests.cs
```

**TC-2.2-07: Should_UpdateTimestamp_When_TemplateUpdated**
```
Type: Integration
Priority: P1
Preconditions: Template exists
Steps:
  1. Note original updated_at
  2. PUT /api/v1/templates/{id}
  3. Check new updated_at
Expected Result: updated_at > original updated_at
Test File: tests/EaaS.Integration.Tests/Features/Templates/UpdateTemplateTests.cs
```

---

### Suppression List (Cross-Cutting -- tested via US-1.1 and infrastructure tests)

> Focus: Redis cache for O(1) lookup, database persistence, suppression check in send flow.

---

**TC-SUP-01: Should_ReturnTrue_When_EmailIsSuppressed**
```
Type: Unit
Priority: P0
Preconditions: RedisSuppressionCache with mock Redis containing "suppressed@test.com"
Steps:
  1. Call ISuppressionCache.IsSuppressedAsync("suppressed@test.com")
Expected Result: Returns true
Test File: tests/EaaS.Infrastructure.Tests/Caching/RedisSuppressionCacheTests.cs
```

**TC-SUP-02: Should_ReturnFalse_When_EmailNotSuppressed**
```
Type: Unit
Priority: P0
Preconditions: RedisSuppressionCache with empty Redis
Steps:
  1. Call ISuppressionCache.IsSuppressedAsync("valid@test.com")
Expected Result: Returns false
Test File: tests/EaaS.Infrastructure.Tests/Caching/RedisSuppressionCacheTests.cs
```

**TC-SUP-03: Should_AddToRedisAndDb_When_AddressAddedToSuppressionList**
```
Type: Integration
Priority: P0
Preconditions: Redis + PostgreSQL Testcontainers running
Steps:
  1. Add "bounce@test.com" to suppression list with reason "hard_bounce"
  2. Check Redis for key
  3. Check database for record
Expected Result: Both Redis and DB contain the suppression entry
Test File: tests/EaaS.Infrastructure.Tests/Caching/RedisSuppressionCacheTests.cs
```

**TC-SUP-04: Should_PerformO1Lookup_When_CheckingRedisCache**
```
Type: Integration
Priority: P2
Preconditions: 10,000 suppressed emails in Redis
Steps:
  1. Time the lookup for a specific email
Expected Result: Lookup completes in < 5ms
Test File: tests/EaaS.Infrastructure.Tests/Caching/RedisSuppressionCacheTests.cs
```

---

### Global Exception Handler Tests

---

**TC-GEH-01: Should_Return400_When_DomainExceptionThrown**
```
Type: Unit
Priority: P0
Preconditions: GlobalExceptionHandler configured
Steps:
  1. Throw DomainException from handler
Expected Result: 400 response with error details
Test File: tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs
```

**TC-GEH-02: Should_Return422_When_RecipientSuppressedExceptionThrown**
```
Type: Unit
Priority: P0
Preconditions: GlobalExceptionHandler configured
Steps:
  1. Throw RecipientSuppressedException
Expected Result: 422 with RECIPIENT_SUPPRESSED error code
Test File: tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs
```

**TC-GEH-03: Should_Return422_When_DomainNotVerifiedExceptionThrown**
```
Type: Unit
Priority: P0
Preconditions: GlobalExceptionHandler configured
Steps:
  1. Throw DomainNotVerifiedException
Expected Result: 422 with DOMAIN_NOT_VERIFIED error code
Test File: tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs
```

**TC-GEH-04: Should_Return404_When_TemplateNotFoundExceptionThrown**
```
Type: Unit
Priority: P0
Preconditions: GlobalExceptionHandler configured
Steps:
  1. Throw TemplateNotFoundException
Expected Result: 404 with NOT_FOUND error code
Test File: tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs
```

**TC-GEH-05: Should_Return409_When_DuplicateDomainExceptionThrown**
```
Type: Unit
Priority: P0
Preconditions: GlobalExceptionHandler configured
Steps:
  1. Throw DuplicateDomainException
Expected Result: 409 with CONFLICT error code
Test File: tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs
```

**TC-GEH-06: Should_Return500_When_UnhandledExceptionThrown**
```
Type: Unit
Priority: P0
Preconditions: GlobalExceptionHandler configured
Steps:
  1. Throw System.Exception("unexpected")
Expected Result: 500 with INTERNAL_ERROR code; exception details NOT exposed in response
Test File: tests/EaaS.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs
```

---

### Domain Entity / Value Object Tests

---

**TC-VO-01: Should_CreateValidEmailAddress_When_FormatCorrect**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create EmailAddress("test@example.com", "Test User")
Expected Result: Address and Name properties set correctly
Test File: tests/EaaS.Api.Tests/Domain/ValueObjects/EmailAddressTests.cs
```

**TC-VO-02: Should_ThrowDomainException_When_EmailFormatInvalid**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Create EmailAddress("not-an-email")
Expected Result: DomainException thrown
Test File: tests/EaaS.Api.Tests/Domain/ValueObjects/EmailAddressTests.cs
```

**TC-VO-03: Should_GenerateMessageId_When_Called**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Call MessageId.New()
Expected Result: Value starts with "msg_", total length 16, alphanumeric after prefix
Test File: tests/EaaS.Api.Tests/Domain/ValueObjects/MessageIdTests.cs
```

**TC-VO-04: Should_GenerateTemplateId_When_Called**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Call TemplateId.New()
Expected Result: Value starts with "tmpl_", total length 13
Test File: tests/EaaS.Api.Tests/Domain/ValueObjects/TemplateIdTests.cs
```

**TC-VO-05: Should_GenerateDomainId_When_Called**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Call DomainId.New()
Expected Result: Value starts with "dom_", total length 10
Test File: tests/EaaS.Api.Tests/Domain/ValueObjects/DomainIdTests.cs
```

**TC-VO-06: Should_GenerateKeyId_When_Called**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Call KeyId.New()
Expected Result: Value starts with "key_", total length 12
Test File: tests/EaaS.Api.Tests/Domain/ValueObjects/KeyIdTests.cs
```

**TC-VO-07: Should_ComputeSha256Hash_When_StringProvided**
```
Type: Unit
Priority: P0
Preconditions: None
Steps:
  1. Call "test_string".ToSha256Hash()
Expected Result: Returns correct 64-character hex SHA-256 digest
Test File: tests/EaaS.Api.Tests/Shared/StringExtensionsTests.cs
```

---

### Repository Integration Tests

---

**TC-REPO-01: Should_SaveAndRetrieveEmail_When_Added**
```
Type: Integration
Priority: P0
Preconditions: PostgreSQL Testcontainer with migrations
Steps:
  1. Create Email entity
  2. Save via IEmailRepository
  3. Retrieve by message_id
Expected Result: Retrieved email matches saved email
Test File: tests/EaaS.Infrastructure.Tests/Persistence/EmailRepositoryTests.cs
```

**TC-REPO-02: Should_SaveAndRetrieveTemplate_When_Added**
```
Type: Integration
Priority: P0
Preconditions: PostgreSQL Testcontainer
Steps:
  1. Create Template entity
  2. Save via ITemplateRepository
  3. Retrieve by ID
Expected Result: Template fields match including html_body and variables_schema
Test File: tests/EaaS.Infrastructure.Tests/Persistence/TemplateRepositoryTests.cs
```

**TC-REPO-03: Should_ExcludeDeletedTemplates_When_Listing**
```
Type: Integration
Priority: P0
Preconditions: Two templates: one active, one soft-deleted
Steps:
  1. Query all templates via repository
Expected Result: Only active template returned; soft-deleted excluded
Test File: tests/EaaS.Infrastructure.Tests/Persistence/TemplateRepositoryTests.cs
```

**TC-REPO-04: Should_SaveAndRetrieveDomain_When_Added**
```
Type: Integration
Priority: P0
Preconditions: PostgreSQL Testcontainer
Steps:
  1. Create Domain entity with DNS records
  2. Save via IDomainRepository
  3. Retrieve by ID
Expected Result: Domain and related DNS records retrieved correctly
Test File: tests/EaaS.Infrastructure.Tests/Persistence/DomainRepositoryTests.cs
```

**TC-REPO-05: Should_EnforceUniqueDomainName_When_DuplicateAdded**
```
Type: Integration
Priority: P0
Preconditions: Domain "test.com" exists for tenant
Steps:
  1. Attempt to save another domain "test.com" for same tenant
Expected Result: Database constraint violation / exception thrown
Test File: tests/EaaS.Infrastructure.Tests/Persistence/DomainRepositoryTests.cs
```

**TC-REPO-06: Should_SaveAndRetrieveApiKey_When_Added**
```
Type: Integration
Priority: P0
Preconditions: PostgreSQL Testcontainer
Steps:
  1. Create ApiKey entity with hash
  2. Save via IApiKeyRepository
  3. Retrieve by hash
Expected Result: ApiKey found with correct status and metadata
Test File: tests/EaaS.Infrastructure.Tests/Persistence/ApiKeyRepositoryTests.cs
```

**TC-REPO-07: Should_UpdateApiKeyStatus_When_Revoked**
```
Type: Integration
Priority: P0
Preconditions: Active API key in DB
Steps:
  1. Update status to Revoked, set revoked_at
  2. Retrieve by hash
Expected Result: Status = Revoked; revoked_at set
Test File: tests/EaaS.Infrastructure.Tests/Persistence/ApiKeyRepositoryTests.cs
```

**TC-REPO-08: Should_SaveSuppressionEntry_When_Added**
```
Type: Integration
Priority: P0
Preconditions: PostgreSQL Testcontainer
Steps:
  1. Save SuppressionEntry for "blocked@test.com" with reason HardBounce
  2. Query by email address
Expected Result: Entry found with correct reason and timestamp
Test File: tests/EaaS.Infrastructure.Tests/Persistence/SuppressionRepositoryTests.cs
```

**TC-REPO-09: Should_EnforceUniqueSuppressionPerTenant_When_DuplicateAdded**
```
Type: Integration
Priority: P1
Preconditions: Suppression entry exists for "blocked@test.com"
Steps:
  1. Attempt to add same email again
Expected Result: Constraint violation or idempotent upsert
Test File: tests/EaaS.Infrastructure.Tests/Persistence/SuppressionRepositoryTests.cs
```

---

### E2E Scenario Tests

---

**TC-E2E-01: Should_CompleteFullSendFlow_When_ApiToWorkerToSes**
```
Type: E2E
Priority: P0
Preconditions: Full stack with Testcontainers, verified domain, active API key, FakeSES
Steps:
  1. POST /api/v1/emails/send
  2. Wait for worker to process (up to 10s)
  3. GET /api/v1/emails/{message_id}
  4. Verify FakeSES received the email
Expected Result: Status = "sent"; FakeSES has email with correct from/to/subject/body; events include "queued" and "sent"
Test File: tests/EaaS.Integration.Tests/Scenarios/SendEmailE2ETests.cs
```

**TC-E2E-02: Should_CompleteApiKeyCreateAuthenticateSendFlow**
```
Type: E2E
Priority: P0
Preconditions: Bootstrap key for creating new keys; verified domain
Steps:
  1. POST /api/v1/keys to create new key
  2. Use new key to POST /api/v1/emails/send
  3. Verify email accepted
Expected Result: New key works immediately for sending
Test File: tests/EaaS.Integration.Tests/Scenarios/ApiKeyFlowE2ETests.cs
```

**TC-E2E-03: Should_CompleteDomainAddVerifySendFlow**
```
Type: E2E
Priority: P0
Preconditions: FakeDnsVerifier configured to pass; FakeSES
Steps:
  1. POST /api/v1/domains to add domain
  2. POST /api/v1/domains/{id}/verify
  3. POST /api/v1/emails/send with from address on that domain
Expected Result: Domain verified; email sent successfully
Test File: tests/EaaS.Integration.Tests/Scenarios/DomainVerificationE2ETests.cs
```

**TC-E2E-04: Should_CompleteTemplateCreateThenSendFlow**
```
Type: E2E
Priority: P0
Preconditions: Verified domain, active API key
Steps:
  1. POST /api/v1/templates to create template
  2. POST /api/v1/emails/send with template_id and variables
  3. Wait for worker to process
  4. Verify FakeSES received rendered email
Expected Result: Email body contains rendered template output with substituted variables
Test File: tests/EaaS.Integration.Tests/Scenarios/TemplateEmailE2ETests.cs
```

**TC-E2E-05: Should_RejectSendFromUnverifiedDomain_EndToEnd**
```
Type: E2E
Priority: P0
Preconditions: Domain added but NOT verified
Steps:
  1. POST /api/v1/emails/send with from address on unverified domain
Expected Result: 422 DOMAIN_NOT_VERIFIED; email not queued; FakeSES not called
Test File: tests/EaaS.Integration.Tests/Scenarios/DomainVerificationE2ETests.cs
```

---

## 5. Sprint 1 Test Summary

### Test Count by Story

| Story | Unit Tests | Integration Tests | E2E Tests | Total |
|-------|-----------|-------------------|-----------|-------|
| US-0.1: Project Scaffolding | 0 | 2 | 0 | 2 |
| US-0.2: DB Schema & Migrations | 0 | 4 | 0 | 4 |
| US-0.3: Redis & RabbitMQ Config | 0 | 4 | 1 | 5 |
| US-0.4: Health Check Endpoint | 0 | 7 | 0 | 7 |
| US-0.5: Structured Logging | 0 | 5 | 0 | 5 |
| US-0.6: Environment Config | 7 | 0 | 0 | 7 |
| US-5.1: Create API Key | 5 | 6 | 0 | 11 |
| US-5.3: Revoke API Key | 5 | 3 | 1 | 9 |
| US-3.1: Add Sending Domain | 5 | 6 | 0 | 11 |
| US-3.2: Verify Domain DNS | 3 | 5 | 0 | 8 |
| US-1.1: Send Single Email | 19 | 9 | 2 | 30 |
| US-1.3: Send with Template | 6 | 3 | 0 | 9 |
| US-2.1: Create Template | 5 | 6 | 0 | 11 |
| US-2.2: Update Template | 0 | 7 | 0 | 7 |
| Suppression (cross-cutting) | 2 | 2 | 0 | 4 |
| Exception Handler (cross-cutting) | 6 | 0 | 0 | 6 |
| Value Objects (cross-cutting) | 7 | 0 | 0 | 7 |
| Repositories (cross-cutting) | 0 | 9 | 0 | 9 |
| E2E Scenarios | 0 | 0 | 5 | 5 |
| **TOTAL** | **70** | **78** | **9** | **157** |

### Test Count by Priority

| Priority | Count | Description |
|----------|-------|-------------|
| **P0** | 98 | Must pass for MVP -- blockers |
| **P1** | 50 | Should pass -- important quality |
| **P2** | 9 | Nice to have -- polish |

### Test Count by Type

| Type | Count | Target Run Time |
|------|-------|----------------|
| Unit | 70 | < 5 seconds |
| Integration | 78 | < 60 seconds |
| E2E | 9 | < 120 seconds |
| **Total** | **157** | **< 3 minutes** |

### Test File Summary

```
tests/
├── EaaS.Api.Tests/
│   ├── Configuration/
│   │   ├── DatabaseOptionsTests.cs
│   │   ├── OptionsBindingTests.cs
│   │   └── OptionsValidationTests.cs
│   ├── Domain/
│   │   └── ValueObjects/
│   │       ├── EmailAddressTests.cs
│   │       ├── MessageIdTests.cs
│   │       ├── TemplateIdTests.cs
│   │       ├── DomainIdTests.cs
│   │       └── KeyIdTests.cs
│   ├── Features/
│   │   ├── ApiKeys/
│   │   │   ├── ApiKeyGeneratorTests.cs
│   │   │   ├── ApiKeyHashTests.cs
│   │   │   └── CreateApiKeyValidatorTests.cs
│   │   ├── Domains/
│   │   │   ├── AddDomainCommandHandlerTests.cs
│   │   │   ├── AddDomainValidatorTests.cs
│   │   │   └── VerifyDomainCommandHandlerTests.cs
│   │   ├── Emails/
│   │   │   ├── SendEmailCommandHandlerTests.cs
│   │   │   ├── SendEmailValidatorTests.cs
│   │   │   └── GetEmailQueryHandlerTests.cs
│   │   └── Templates/
│   │       ├── CreateTemplateCommandHandlerTests.cs
│   │       └── CreateTemplateValidatorTests.cs
│   ├── Helpers/
│   │   └── IdGeneratorTests.cs
│   ├── Middleware/
│   │   ├── ApiKeyAuthenticationHandlerTests.cs
│   │   ├── RateLimitingMiddlewareTests.cs
│   │   └── GlobalExceptionHandlerTests.cs
│   └── Shared/
│       └── StringExtensionsTests.cs
├── EaaS.Worker.Tests/
│   └── Consumers/
│       └── SendEmailConsumerTests.cs
├── EaaS.Infrastructure.Tests/
│   ├── Caching/
│   │   ├── RedisConnectionTests.cs
│   │   └── RedisSuppressionCacheTests.cs
│   ├── Messaging/
│   │   ├── RabbitMqConnectionTests.cs
│   │   └── QueueTopologyTests.cs
│   ├── Persistence/
│   │   ├── MigrationTests.cs
│   │   ├── EmailRepositoryTests.cs
│   │   ├── TemplateRepositoryTests.cs
│   │   ├── DomainRepositoryTests.cs
│   │   ├── ApiKeyRepositoryTests.cs
│   │   └── SuppressionRepositoryTests.cs
│   └── Templating/
│       └── FluidTemplateRendererTests.cs
└── EaaS.Integration.Tests/
    ├── Builders/
    │   ├── SendEmailRequestBuilder.cs
    │   ├── CreateTemplateRequestBuilder.cs
    │   ├── AddDomainRequestBuilder.cs
    │   ├── CreateApiKeyRequestBuilder.cs
    │   ├── EmailEntityBuilder.cs
    │   ├── TemplateEntityBuilder.cs
    │   ├── DomainEntityBuilder.cs
    │   └── ApiKeyEntityBuilder.cs
    ├── Fakes/
    │   ├── FakeSesService.cs
    │   └── FakeDnsVerifier.cs
    ├── Fixtures/
    │   ├── PostgresContainerFixture.cs
    │   ├── RedisContainerFixture.cs
    │   ├── RabbitMqContainerFixture.cs
    │   ├── IntegrationTestFixture.cs
    │   └── EaaSWebApplicationFactory.cs
    ├── Helpers/
    │   ├── AuthenticatedHttpClient.cs
    │   ├── TestApiKeySeeder.cs
    │   └── JsonHelper.cs
    ├── Infrastructure/
    │   ├── DependencyInjectionTests.cs
    │   ├── StartupTests.cs
    │   └── LoggingTests.cs
    ├── Features/
    │   ├── ApiKeys/
    │   │   ├── CreateApiKeyTests.cs
    │   │   └── RevokeApiKeyTests.cs
    │   ├── Domains/
    │   │   ├── AddDomainTests.cs
    │   │   └── VerifyDomainTests.cs
    │   ├── Emails/
    │   │   ├── SendEmailTests.cs
    │   │   ├── SendTemplateEmailTests.cs
    │   │   └── GetEmailTests.cs
    │   ├── Health/
    │   │   └── HealthCheckTests.cs
    │   └── Templates/
    │       ├── CreateTemplateTests.cs
    │       └── UpdateTemplateTests.cs
    └── Scenarios/
        ├── SendEmailE2ETests.cs
        ├── ApiKeyFlowE2ETests.cs
        ├── DomainVerificationE2ETests.cs
        ├── TemplateEmailE2ETests.cs
        └── RevokedKeyQueuedEmailTests.cs
```

---

## Appendix: Test Execution Order

Sprint 1 TDD implementation should follow this order to respect dependencies:

1. **Value Objects & Helpers** (TC-VO-*, TC-1.1-22) -- no dependencies
2. **Configuration Options** (TC-0.6-*) -- no dependencies
3. **Domain Entity Tests** -- depends on value objects
4. **Validators** (all *ValidatorTests) -- depends on request DTOs
5. **Exception Handler** (TC-GEH-*) -- depends on domain exceptions
6. **Repository Tests** (TC-REPO-*) -- requires Testcontainers setup
7. **Redis Cache Tests** (TC-SUP-*) -- requires Redis Testcontainer
8. **Auth Middleware** (TC-5.3-06 through TC-5.3-10) -- depends on repository interface
9. **Rate Limiting** (TC-5.3-11, TC-5.3-12) -- depends on Redis
10. **API Key CRUD** (TC-5.1-*, TC-5.3-*) -- depends on auth + repository
11. **Domain CRUD** (TC-3.1-*, TC-3.2-*) -- depends on auth + SES mock
12. **Template CRUD** (TC-2.1-*, TC-2.2-*) -- depends on auth + template renderer
13. **Template Renderer** (TC-1.3-08 through TC-1.3-12) -- depends on Fluid library
14. **Send Email** (TC-1.1-*) -- depends on all above
15. **Worker Consumer** (TC-1.1-26 through TC-1.1-30, TC-1.3-04, TC-1.3-06) -- depends on SES mock + template renderer
16. **Health Checks** (TC-0.4-*) -- depends on all infrastructure
17. **Logging** (TC-0.5-*) -- cross-cutting, run alongside
18. **E2E Scenarios** (TC-E2E-*) -- last, requires everything working
