# EaaS - Technical Architecture Specification

**Version:** 1.1
**Date:** 2026-03-27
**Author:** Senior Architect
**Sprint:** 1 (MVP)
**Status:** Ready for Developer Handoff

## Revision History

| Version | Date | Description |
|---------|------|-------------|
| 1.0 | 2026-03-27 | Initial architecture specification |
| 1.1 | 2026-03-27 | Applied Gate 1 review fixes (6 required changes + 5 recommendations). Key changes: removed table partitioning, switched to MassTransit, aligned Sprint 1 acceptance criteria with backlog, added API key bootstrap mechanism, fixed SES IAM policy for v2 SDK, switched to classic durable queues, dropped AutoMapper, deferred template versioning, added `updated_at` to `dns_records`, switched healthcheck to `wget`, documented server-to-server API (no CORS). |

---

## Table of Contents

1. [Architecture Decisions](#1-architecture-decisions)
2. [Solution Structure](#2-solution-structure)
3. [Database Schema (DDL)](#3-database-schema-ddl)
4. [API Specification](#4-api-specification)
5. [Message Queue Design](#5-message-queue-design)
6. [AWS SES Integration Design](#6-aws-ses-integration-design)
7. [Docker Compose](#7-docker-compose)
8. [Configuration Design](#8-configuration-design)
9. [NuGet Packages](#9-nuget-packages)
10. [Acceptance Criteria for Sprint 1](#10-acceptance-criteria-for-sprint-1)

---

## 1. Architecture Decisions

### ADR-001: Minimal API over Controllers
**Decision:** Use .NET 8 Minimal API with endpoint grouping instead of MVC controllers.
**Why:** Less boilerplate, faster startup, smaller memory footprint. Sprint 1 has ~16 endpoints -- controllers add no value at this scale. Endpoint groups provide the same logical separation.

### ADR-002: Vertical Slice Organization for API
**Decision:** Organize API features as vertical slices (Features/{Feature}/{Action}), not horizontal layers in the API project.
**Why:** Each endpoint is a self-contained unit with its own request/response/handler. Reduces cross-file navigation. The Domain and Infrastructure projects handle shared concerns.

### ADR-003: MediatR for Request Pipeline
**Decision:** Use MediatR to decouple endpoint definitions from business logic.
**Why:** Enables clean request validation (FluentValidation pipeline behavior), logging, and unit testing of handlers in isolation. The endpoint becomes a thin HTTP adapter.

### ADR-004: Outbox Pattern Skipped for Sprint 1
**Decision:** Publish to RabbitMQ directly from the API handler, without a transactional outbox.
**Why:** 24-hour MVP deadline. The outbox pattern prevents message loss during DB-commit-then-publish failures, but adds significant complexity. Acceptable risk for Sprint 1 -- if the publish fails, the API returns 500 and the client retries. Revisit in Sprint 2.

### ADR-005: EF Core for All Tables (Partitioning Deferred)
**Decision:** Use EF Core 8 for all CRUD operations. Tables are regular (non-partitioned) in Sprint 1.
**Why:** EF Core does not natively support PostgreSQL table partitioning. Partitioning adds composite PK complexity, prevents standard FK constraints, and complicates queries -- all for no benefit at MVP volumes (<5K emails/month). Partitioning can be added in a future sprint when volume justifies the complexity.

### ADR-006: Fluid (Liquid) for Template Rendering
**Decision:** Use the Fluid library (Liquid syntax) for template rendering.
**Why:** BRD specifies Liquid syntax. Fluid is the fastest .NET Liquid implementation (10x faster than DotLiquid). Actively maintained, supports custom filters, and handles untrusted templates safely with configurable limits.

### ADR-007: Single Tenant ID Column Present But Unused
**Decision:** Include `tenant_id` column in all tables, defaulting to a fixed GUID for Sprint 1.
**Why:** The BRD anticipates multi-tenant in Phase 3. Adding the column now avoids a painful migration later. All queries will filter by tenant_id from day one, so the transition is seamless.

### ADR-008: Webhook Processor as Separate Service
**Decision:** The SNS webhook receiver runs as its own container (EaaS.WebhookProcessor) rather than inside the API or Worker.
**Why:** SNS webhooks are inbound HTTP requests that must be publicly accessible. Isolating them limits the attack surface -- the main API authenticates via API keys while the webhook processor validates SNS message signatures. Different scaling and security profiles.

---

## 2. Solution Structure

```
eaas/
├── eaas.sln
├── .editorconfig
├── .gitignore
├── Directory.Build.props                  # Shared MSBuild properties (target framework, nullable, implicit usings)
├── Directory.Packages.props               # Central package version management
├── docker-compose.yml
├── docker-compose.override.yml            # Local dev overrides (ports, volumes)
├── .env.example                           # Template for environment variables
├── nginx/
│   ├── nginx.conf                         # Reverse proxy config with TLS
│   └── ssl/                               # TLS cert mount point (empty in repo)
├── scripts/
│   ├── init-db.sql                        # Initial database + role creation
│   └── seed-data.sql                      # Dev seed data (test API key, domain, template)
├── docs/
│   ├── BRD_PRD.md
│   ├── ARCHITECTURE.md
│   └── API.md                             # Generated OpenAPI spec reference
├── src/
│   ├── EaaS.Domain/
│   ├── EaaS.Infrastructure/
│   ├── EaaS.Shared/
│   ├── EaaS.Api/
│   ├── EaaS.Worker/
│   ├── EaaS.WebhookProcessor/
│   └── dashboard/                        # Next.js 15 dashboard (separate from .NET solution)
└── tests/
    ├── EaaS.Api.Tests/
    ├── EaaS.Worker.Tests/
    ├── EaaS.Infrastructure.Tests/
    └── EaaS.Integration.Tests/
```

---

### 2.1 EaaS.Domain

The pure domain layer. Zero dependencies on infrastructure, frameworks, or NuGet packages (except for annotations). This project defines WHAT the system is.

```
EaaS.Domain/
├── EaaS.Domain.csproj
├── Entities/
│   ├── Tenant.cs                          # Tenant aggregate root (Id, Name, CreatedAt)
│   ├── ApiKey.cs                          # API key entity (Id, TenantId, Name, KeyHash, Prefix, AllowedDomains, Status, CreatedAt, LastUsedAt, RevokedAt)
│   ├── Domain.cs                          # Sending domain entity (Id, TenantId, DomainName, Status, DnsRecords, CreatedAt, VerifiedAt)
│   ├── DnsRecord.cs                       # Value object for DNS record (Type, Name, Value, Purpose, IsVerified)
│   ├── Template.cs                        # Email template entity (Id, TenantId, Name, SubjectTemplate, HtmlBody, TextBody, VariablesSchema, Version, CreatedAt, UpdatedAt, DeletedAt)
│   ├── Email.cs                           # Email aggregate root (Id, TenantId, ApiKeyId, MessageId, From, To, Cc, Bcc, Subject, HtmlBody, TextBody, TemplateId, Variables, Attachments, Tags, Metadata, TrackOpens, TrackClicks, Status, CreatedAt, SentAt, DeliveredAt)
│   ├── EmailEvent.cs                      # Email lifecycle event (Id, EmailId, EventType, Timestamp, Data)
│   ├── SuppressionEntry.cs                # Suppression list entry (Id, TenantId, EmailAddress, Reason, SourceMessageId, SuppressedAt)
│   └── Webhook.cs                         # Webhook endpoint config (Id, TenantId, Url, Events, Secret, Status, CreatedAt)
├── Enums/
│   ├── EmailStatus.cs                     # Queued, Sending, Sent, Delivered, Bounced, Complained, Failed
│   ├── EventType.cs                       # Queued, Sent, Delivered, Bounced, Complained, Opened, Clicked, Failed
│   ├── DomainStatus.cs                    # PendingVerification, Verified, Failed, Suspended
│   ├── ApiKeyStatus.cs                    # Active, Revoked, Rotating
│   ├── SuppressionReason.cs               # HardBounce, SoftBounceLimit, Complaint, Manual
│   ├── DnsRecordType.cs                   # TXT, CNAME, MX
│   └── DnsRecordPurpose.cs                # SPF, DKIM, DMARC
├── ValueObjects/
│   ├── EmailAddress.cs                    # Validated email address value object (Address, Name)
│   ├── MessageId.cs                       # Strongly-typed message ID (msg_ prefix + 12 alphanumeric)
│   ├── BatchId.cs                         # Strongly-typed batch ID (batch_ prefix + 8 alphanumeric)
│   ├── TemplateId.cs                      # Strongly-typed template ID (tmpl_ prefix + 8 alphanumeric)
│   ├── DomainId.cs                        # Strongly-typed domain ID (dom_ prefix + 6 alphanumeric)
│   └── KeyId.cs                           # Strongly-typed key ID (key_ prefix + 8 alphanumeric)
├── Interfaces/
│   ├── IEmailRepository.cs                # Email CRUD + query interface
│   ├── ITemplateRepository.cs             # Template CRUD interface
│   ├── IDomainRepository.cs               # Domain CRUD interface
│   ├── IApiKeyRepository.cs               # API key CRUD interface
│   ├── ISuppressionRepository.cs          # Suppression list interface
│   ├── IWebhookRepository.cs              # Webhook config interface
│   ├── IUnitOfWork.cs                     # Transaction commit interface
│   ├── IMessagePublisher.cs               # Publish message to queue (abstraction over RabbitMQ)
│   ├── IEmailDeliveryService.cs           # Send email via provider (abstraction over SES)
│   ├── ITemplateRenderer.cs               # Render Liquid template to HTML/text
│   ├── IDnsVerifier.cs                    # Verify DNS records for a domain
│   ├── ISuppressionCache.cs               # Fast suppression lookup (abstraction over Redis)
│   └── IDateTimeProvider.cs               # Testable clock abstraction
└── Exceptions/
    ├── DomainException.cs                 # Base domain exception
    ├── RecipientSuppressedException.cs    # Thrown when sending to suppressed address
    ├── DomainNotVerifiedException.cs      # Thrown when sending from unverified domain
    ├── TemplateNotFoundException.cs        # Template ID does not exist
    ├── TemplateRenderException.cs          # Liquid rendering failure
    ├── DuplicateDomainException.cs         # Domain already registered
    └── AttachmentTooLargeException.cs      # Attachment exceeds size limit
```

---

### 2.2 EaaS.Infrastructure

Implements all domain interfaces. Owns EF Core, RabbitMQ, Redis, SES, and DNS clients.

```
EaaS.Infrastructure/
├── EaaS.Infrastructure.csproj
├── Persistence/
│   ├── EaaSDbContext.cs                   # EF Core DbContext with all DbSets, OnModelCreating configuration
│   ├── UnitOfWork.cs                      # Implements IUnitOfWork (wraps DbContext.SaveChangesAsync)
│   ├── Configurations/
│   │   ├── TenantConfiguration.cs         # EF Core entity configuration for Tenant
│   │   ├── ApiKeyConfiguration.cs         # EF Core entity configuration for ApiKey
│   │   ├── DomainConfiguration.cs         # EF Core entity configuration for Domain
│   │   ├── TemplateConfiguration.cs       # EF Core entity configuration for Template
│   │   ├── EmailConfiguration.cs          # EF Core entity configuration for Email
│   │   ├── EmailEventConfiguration.cs     # EF Core entity configuration for EmailEvent
│   │   ├── SuppressionEntryConfiguration.cs # EF Core entity configuration for SuppressionEntry
│   │   └── WebhookConfiguration.cs        # EF Core entity configuration for Webhook
│   ├── Repositories/
│   │   ├── EmailRepository.cs             # Implements IEmailRepository
│   │   ├── TemplateRepository.cs          # Implements ITemplateRepository
│   │   ├── DomainRepository.cs            # Implements IDomainRepository
│   │   ├── ApiKeyRepository.cs            # Implements IApiKeyRepository
│   │   ├── SuppressionRepository.cs       # Implements ISuppressionRepository
│   │   └── WebhookRepository.cs           # Implements IWebhookRepository
│   └── Migrations/
│       └── (EF Core auto-generated)
├── Messaging/
│   ├── MassTransitPublisher.cs            # Implements IMessagePublisher -- publishes via MassTransit IBus
│   ├── MassTransitConfiguration.cs        # MassTransit + RabbitMQ bus configuration (retry, DLQ, consumer registration)
│   └── Messages/
│       ├── SendEmailMessage.cs            # Message schema for email send queue
│       └── ProcessWebhookMessage.cs       # Message schema for webhook delivery queue
├── Email/
│   ├── SesEmailDeliveryService.cs         # Implements IEmailDeliveryService -- sends via AWS SES SDK
│   ├── SesConfiguration.cs                # SES region, credentials config POCO
│   └── SesHealthCheck.cs                  # IHealthCheck implementation that pings SES GetSendQuota
├── Dns/
│   ├── DnsVerifier.cs                     # Implements IDnsVerifier -- uses DnsClient.NET to verify SPF/DKIM/DMARC
│   └── SesDomainIdentityService.cs        # Calls SES VerifyDomainIdentity and VerifyDomainDkim
├── Caching/
│   ├── RedisSuppressionCache.cs           # Implements ISuppressionCache -- SET/GET suppressed emails in Redis
│   ├── RedisTemplateCache.cs              # Caches rendered templates in Redis with TTL
│   └── RedisRateLimiter.cs                # Sliding window rate limiter using Redis sorted sets
├── Templating/
│   └── FluidTemplateRenderer.cs           # Implements ITemplateRenderer -- renders Liquid templates via Fluid
├── Services/
│   └── UtcDateTimeProvider.cs             # Implements IDateTimeProvider -- returns DateTime.UtcNow
├── HealthChecks/
│   ├── RabbitMqHealthCheck.cs             # IHealthCheck for RabbitMQ connection
│   └── RedisHealthCheck.cs                # IHealthCheck for Redis connection
└── DependencyInjection.cs                 # Extension method: services.AddInfrastructure(configuration)
```

---

### 2.3 EaaS.Shared

Cross-cutting concerns shared by all projects. No business logic.

```
EaaS.Shared/
├── EaaS.Shared.csproj
├── Models/
│   ├── ApiResponse.cs                     # Generic wrapper: { success: bool, data: T }
│   ├── ApiErrorResponse.cs                # Error wrapper: { success: false, error: { code, message, details[] } }
│   ├── ErrorDetail.cs                     # { field: string, message: string }
│   ├── PagedRequest.cs                    # { page: int, page_size: int, sort_by: string, sort_dir: string }
│   └── PagedResponse<T>.cs               # { data: T[], total: int, page: int, page_size: int, total_pages: int }
├── Constants/
│   ├── ErrorCodes.cs                      # String constants: VALIDATION_ERROR, NOT_FOUND, UNAUTHORIZED, RATE_LIMITED, etc.
│   ├── QueueNames.cs                      # String constants: eaas.emails.send, eaas.emails.send.dlq, eaas.webhooks.deliver, etc.
│   └── CacheKeys.cs                       # Redis key patterns: suppression:{email}, template:{id}, ratelimit:{keyId}
├── Extensions/
│   ├── StringExtensions.cs                # GenerateRandomId(prefix, length), ToSha256Hash()
│   └── DateTimeExtensions.cs              # ToUnixTimestamp(), date range helpers
└── Helpers/
    └── IdGenerator.cs                     # Static methods: NewMessageId(), NewBatchId(), NewTemplateId(), NewDomainId(), NewKeyId()
```

---

### 2.4 EaaS.Api

The HTTP API. Thin layer: receives requests, validates, dispatches to MediatR, returns responses.

```
EaaS.Api/
├── EaaS.Api.csproj
├── Program.cs                             # App builder: services, middleware, endpoint mapping, Serilog, Swagger
├── appsettings.json                       # Base configuration
├── appsettings.Development.json           # Dev overrides
├── Dockerfile                             # Multi-stage build: restore, build, publish, runtime
├── Properties/
│   └── launchSettings.json
├── Middleware/
│   ├── ApiKeyAuthenticationHandler.cs     # Custom AuthenticationHandler -- validates Bearer token against hashed API keys
│   ├── RateLimitingMiddleware.cs          # Per-API-key rate limiting via Redis sliding window
│   ├── RequestLoggingMiddleware.cs        # Logs request method, path, status code, duration via Serilog
│   └── GlobalExceptionHandler.cs          # Catches unhandled exceptions, maps domain exceptions to HTTP status codes
├── Features/
│   ├── Emails/
│   │   ├── SendEmail/
│   │   │   ├── SendEmailEndpoint.cs       # POST /api/v1/emails/send -- maps request to MediatR command
│   │   │   ├── SendEmailCommand.cs        # MediatR IRequest<SendEmailResponse>
│   │   │   ├── SendEmailCommandHandler.cs # Validates domain, checks suppression, publishes to queue, saves to DB
│   │   │   ├── SendEmailRequest.cs        # Request DTO (from, to, cc, bcc, subject, html_body, text_body, template_id, variables, attachments, tags, metadata, track_opens, track_clicks)
│   │   │   ├── SendEmailResponse.cs       # Response DTO (message_id, status, queued_at)
│   │   │   └── SendEmailValidator.cs      # FluentValidation rules for SendEmailRequest
│   │   ├── SendBatch/
│   │   │   ├── SendBatchEndpoint.cs       # POST /api/v1/emails/batch
│   │   │   ├── SendBatchCommand.cs        # MediatR command
│   │   │   ├── SendBatchCommandHandler.cs # Validates each email, publishes individually, returns per-item results
│   │   │   ├── SendBatchRequest.cs        # { emails: SendEmailRequest[] }
│   │   │   ├── SendBatchResponse.cs       # { batch_id, total, accepted, rejected, messages[] }
│   │   │   └── SendBatchValidator.cs      # Max 100 items, each item validated
│   │   ├── GetEmail/
│   │   │   ├── GetEmailEndpoint.cs        # GET /api/v1/emails/{id}
│   │   │   ├── GetEmailQuery.cs           # MediatR IRequest<GetEmailResponse>
│   │   │   ├── GetEmailQueryHandler.cs    # Fetches email + events from repository
│   │   │   └── GetEmailResponse.cs        # Full email detail with event timeline
│   │   └── ListEmails/
│   │       ├── ListEmailsEndpoint.cs      # GET /api/v1/emails
│   │       ├── ListEmailsQuery.cs         # MediatR IRequest<PagedResponse<EmailSummaryDto>>
│   │       ├── ListEmailsQueryHandler.cs  # Paginated, filterable email list
│   │       └── EmailSummaryDto.cs         # Summary projection (id, to, from, subject, status, sent_at)
│   ├── Templates/
│   │   ├── CreateTemplate/
│   │   │   ├── CreateTemplateEndpoint.cs  # POST /api/v1/templates
│   │   │   ├── CreateTemplateCommand.cs
│   │   │   ├── CreateTemplateCommandHandler.cs  # Validates Liquid syntax, saves to DB, caches in Redis
│   │   │   ├── CreateTemplateRequest.cs   # { name, subject_template, html_body, text_body, variables_schema }
│   │   │   ├── CreateTemplateResponse.cs  # { template_id, name, version, created_at }
│   │   │   └── CreateTemplateValidator.cs # Name uniqueness, max size 512KB, Liquid syntax check
│   │   ├── GetTemplate/
│   │   │   ├── GetTemplateEndpoint.cs     # GET /api/v1/templates/{id}
│   │   │   ├── GetTemplateQuery.cs
│   │   │   ├── GetTemplateQueryHandler.cs
│   │   │   └── GetTemplateResponse.cs
│   │   ├── ListTemplates/
│   │   │   ├── ListTemplatesEndpoint.cs   # GET /api/v1/templates
│   │   │   ├── ListTemplatesQuery.cs
│   │   │   ├── ListTemplatesQueryHandler.cs
│   │   │   └── TemplateSummaryDto.cs
│   │   ├── UpdateTemplate/
│   │   │   ├── UpdateTemplateEndpoint.cs  # PUT /api/v1/templates/{id}
│   │   │   ├── UpdateTemplateCommand.cs
│   │   │   ├── UpdateTemplateCommandHandler.cs  # Increments version, invalidates Redis cache
│   │   │   ├── UpdateTemplateRequest.cs
│   │   │   └── UpdateTemplateValidator.cs
│   │   └── DeleteTemplate/
│   │       ├── DeleteTemplateEndpoint.cs  # DELETE /api/v1/templates/{id}
│   │       ├── DeleteTemplateCommand.cs
│   │       └── DeleteTemplateCommandHandler.cs  # Soft delete (sets deleted_at), removes from Redis
│   ├── Domains/
│   │   ├── AddDomain/
│   │   │   ├── AddDomainEndpoint.cs       # POST /api/v1/domains
│   │   │   ├── AddDomainCommand.cs
│   │   │   ├── AddDomainCommandHandler.cs # Calls SES VerifyDomainIdentity, generates DNS records, saves to DB
│   │   │   ├── AddDomainRequest.cs        # { domain: string }
│   │   │   ├── AddDomainResponse.cs       # { domain_id, domain, status, dns_records[], created_at }
│   │   │   └── AddDomainValidator.cs      # Domain format validation, duplicate check
│   │   ├── ListDomains/
│   │   │   ├── ListDomainsEndpoint.cs     # GET /api/v1/domains
│   │   │   ├── ListDomainsQuery.cs
│   │   │   ├── ListDomainsQueryHandler.cs
│   │   │   └── DomainSummaryDto.cs
│   │   └── VerifyDomain/
│   │       ├── VerifyDomainEndpoint.cs    # POST /api/v1/domains/{id}/verify
│   │       ├── VerifyDomainCommand.cs
│   │       └── VerifyDomainCommandHandler.cs  # Calls IDnsVerifier, updates status per record
│   ├── ApiKeys/
│   │   ├── CreateApiKey/
│   │   │   ├── CreateApiKeyEndpoint.cs    # POST /api/v1/keys
│   │   │   ├── CreateApiKeyCommand.cs
│   │   │   ├── CreateApiKeyCommandHandler.cs  # Generates key, hashes with SHA-256, stores hash
│   │   │   ├── CreateApiKeyRequest.cs     # { name, allowed_domains[] }
│   │   │   ├── CreateApiKeyResponse.cs    # { key_id, name, api_key, prefix, allowed_domains, created_at } -- api_key shown ONCE
│   │   │   └── CreateApiKeyValidator.cs
│   │   ├── ListApiKeys/
│   │   │   ├── ListApiKeysEndpoint.cs     # GET /api/v1/keys
│   │   │   ├── ListApiKeysQuery.cs
│   │   │   ├── ListApiKeysQueryHandler.cs
│   │   │   └── ApiKeySummaryDto.cs        # { key_id, name, prefix, status, created_at, last_used_at, send_count }
│   │   └── RevokeApiKey/
│   │       ├── RevokeApiKeyEndpoint.cs    # DELETE /api/v1/keys/{id}
│   │       ├── RevokeApiKeyCommand.cs
│   │       └── RevokeApiKeyCommandHandler.cs  # Sets status=Revoked, invalidates Redis cache
│   └── Health/
│       └── HealthEndpoint.cs              # GET /health -- aggregates all IHealthCheck results
├── Mapping/
│   └── EntityMappingExtensions.cs         # Manual mapping extension methods: Entity <-> DTO (no AutoMapper)
└── DependencyInjection.cs                 # Extension method: services.AddApiServices()
```

---

### 2.5 EaaS.Worker

Background worker that consumes messages from RabbitMQ, renders templates, sends via SES, and records results.

```
EaaS.Worker/
├── EaaS.Worker.csproj
├── Program.cs                             # Host builder: registers consumers, Serilog, DI
├── appsettings.json
├── Dockerfile
├── Consumers/
│   ├── SendEmailConsumer.cs               # Consumes from eaas.emails.send queue. Renders template (if template_id), calls SES, updates email status in DB, publishes events.
│   └── WebhookDeliveryConsumer.cs         # Consumes from eaas.webhooks.deliver queue. POSTs to webhook URL with HMAC signature. Handles retries.
├── Services/
│   ├── EmailSendingService.cs             # Orchestrates: check suppression -> render template -> inject tracking pixel -> rewrite links -> send via SES -> record result
│   ├── TrackingPixelInjector.cs           # Injects 1x1 transparent GIF <img> tag before </body> in HTML
│   └── LinkRewriter.cs                    # Rewrites <a href="..."> to pass through tracking endpoint. Skips unsubscribe links.
└── HealthChecks/
    └── WorkerHealthCheck.cs               # Reports consumer connection status
```

---

### 2.6 EaaS.WebhookProcessor

Receives inbound webhooks from AWS SNS (bounces, complaints, deliveries) and processes them.

```
EaaS.WebhookProcessor/
├── EaaS.WebhookProcessor.csproj
├── Program.cs                             # Minimal API host: single POST endpoint for SNS
├── appsettings.json
├── Dockerfile
├── Endpoints/
│   ├── SnsWebhookEndpoint.cs              # POST /webhooks/sns -- receives SNS notifications
│   └── TrackingEndpoint.cs                # GET /track/open/{token} -- tracking pixel hit; GET /track/click/{token} -- link click redirect
├── Services/
│   ├── SnsMessageValidator.cs             # Validates SNS message signature (certificate download + SHA1WithRSA verification)
│   ├── BounceProcessor.cs                 # Parses SES bounce notification, adds to suppression list, updates email status
│   ├── ComplaintProcessor.cs              # Parses SES complaint notification, adds to suppression list, updates email status
│   ├── DeliveryProcessor.cs               # Parses SES delivery notification, updates email status to Delivered
│   └── TrackingTokenService.cs            # Generates and validates HMAC-signed tracking tokens (email_id + url encoded)
└── Models/
    ├── SnsMessage.cs                      # SNS notification envelope DTO
    ├── SesBounceNotification.cs           # SES bounce payload DTO
    ├── SesComplaintNotification.cs        # SES complaint payload DTO
    └── SesDeliveryNotification.cs         # SES delivery payload DTO
```

---

### 2.7 Dashboard (Next.js)

Next.js 15 dashboard with shadcn/ui and Tailwind CSS. Deferred to Sprint 3+ per backlog. Structure defined here for completeness.

```
dashboard/
├── package.json
├── next.config.ts
├── tailwind.config.ts
├── Dockerfile
├── src/
│   ├── app/
│   │   ├── layout.tsx                     # Root layout with sidebar nav + content area
│   │   ├── page.tsx                       # Overview: key metrics cards, 30-day send chart, system health
│   │   ├── login/
│   │   │   └── page.tsx                   # Username/password login (single-user, bcrypt)
│   │   └── emails/
│   │       ├── page.tsx                   # Paginated, filterable email log table
│   │       └── [id]/page.tsx             # Full email detail: status timeline, headers, template, variables
│   ├── components/
│   │   ├── ui/                            # shadcn/ui components
│   │   ├── nav-menu.tsx                   # Sidebar navigation (Overview, Emails, Templates, Domains, API Keys)
│   │   └── layout/
│   │       └── main-layout.tsx            # App shell wrapper
│   └── lib/
│       ├── api-client.ts                  # Calls EaaS.Api internally for data (fetch, no API key -- internal network)
│       └── auth.ts                        # Cookie-based auth, bcrypt password verification
```

---

### 2.8 Test Projects

```
tests/
├── EaaS.Api.Tests/
│   ├── EaaS.Api.Tests.csproj
│   ├── Features/
│   │   ├── Emails/
│   │   │   ├── SendEmailCommandHandlerTests.cs    # Unit tests: valid send, suppressed recipient, unverified domain, missing template
│   │   │   ├── SendBatchCommandHandlerTests.cs    # Unit tests: valid batch, partial failures, max 100 limit
│   │   │   ├── SendEmailValidatorTests.cs         # Validation rule tests
│   │   │   └── GetEmailQueryHandlerTests.cs       # Unit tests: found, not found
│   │   ├── Templates/
│   │   │   ├── CreateTemplateCommandHandlerTests.cs
│   │   │   └── CreateTemplateValidatorTests.cs
│   │   ├── Domains/
│   │   │   └── AddDomainCommandHandlerTests.cs
│   │   └── ApiKeys/
│   │       └── CreateApiKeyCommandHandlerTests.cs
│   └── Middleware/
│       ├── ApiKeyAuthenticationHandlerTests.cs
│       └── RateLimitingMiddlewareTests.cs
├── EaaS.Worker.Tests/
│   ├── EaaS.Worker.Tests.csproj
│   └── Consumers/
│       ├── SendEmailConsumerTests.cs              # Unit tests: successful send, SES failure, template render failure, retry behavior
│       └── WebhookDeliveryConsumerTests.cs
├── EaaS.Infrastructure.Tests/
│   ├── EaaS.Infrastructure.Tests.csproj
│   ├── Persistence/
│   │   ├── EmailRepositoryTests.cs                # Integration tests with Testcontainers PostgreSQL
│   │   └── TemplateRepositoryTests.cs
│   ├── Caching/
│   │   └── RedisSuppressionCacheTests.cs          # Integration tests with Testcontainers Redis
│   └── Templating/
│       └── FluidTemplateRendererTests.cs          # Unit tests: variable substitution, missing vars, syntax errors, max size
└── EaaS.Integration.Tests/
    ├── EaaS.Integration.Tests.csproj
    └── Scenarios/
        ├── SendEmailE2ETests.cs                   # Full flow: API call -> queue -> worker -> DB status update
        └── DomainVerificationE2ETests.cs          # Full flow: add domain -> verify -> send
```

---

## 3. Database Schema (DDL)

All DDL is copy-paste ready. Execute in order.

```sql
-- ============================================================
-- EaaS Database Schema - Sprint 1 MVP
-- PostgreSQL 16
-- ============================================================

-- ============================================================
-- 1. EXTENSIONS
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- 2. CUSTOM TYPES (ENUMS)
-- ============================================================
CREATE TYPE email_status AS ENUM (
    'queued',
    'sending',
    'sent',
    'delivered',
    'bounced',
    'complained',
    'failed'
);

CREATE TYPE event_type AS ENUM (
    'queued',
    'sent',
    'delivered',
    'bounced',
    'complained',
    'opened',
    'clicked',
    'failed'
);

CREATE TYPE domain_status AS ENUM (
    'pending_verification',
    'verified',
    'failed',
    'suspended'
);

CREATE TYPE api_key_status AS ENUM (
    'active',
    'revoked',
    'rotating'
);

CREATE TYPE suppression_reason AS ENUM (
    'hard_bounce',
    'soft_bounce_limit',
    'complaint',
    'manual'
);

CREATE TYPE dns_record_purpose AS ENUM (
    'spf',
    'dkim',
    'dmarc'
);

-- ============================================================
-- 3. TABLES
-- ============================================================

-- -----------------------------------------------------------
-- 3.1 tenants
-- Single tenant for Sprint 1. Column exists for Phase 3.
-- -----------------------------------------------------------
CREATE TABLE tenants (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name            VARCHAR(255) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Insert default tenant for Sprint 1
INSERT INTO tenants (id, name) VALUES (
    '00000000-0000-0000-0000-000000000001',
    'Default'
);

-- -----------------------------------------------------------
-- 3.2 api_keys
-- API key hashes. The plaintext key is never stored.
-- -----------------------------------------------------------
CREATE TABLE api_keys (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    name            VARCHAR(255) NOT NULL,
    key_hash        VARCHAR(64) NOT NULL,              -- SHA-256 hex digest (64 chars)
    prefix          VARCHAR(16) NOT NULL,               -- First 8 chars of the key for identification
    allowed_domains TEXT[] DEFAULT '{}',                 -- Array of domain names this key can send from; empty = all
    status          api_key_status NOT NULL DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_used_at    TIMESTAMPTZ,
    revoked_at      TIMESTAMPTZ,
    CONSTRAINT uq_api_keys_key_hash UNIQUE (key_hash)
);

CREATE INDEX idx_api_keys_tenant ON api_keys(tenant_id);
CREATE INDEX idx_api_keys_status ON api_keys(tenant_id, status) WHERE status = 'active';

-- -----------------------------------------------------------
-- 3.3 domains
-- Sending domains with verification status.
-- -----------------------------------------------------------
CREATE TABLE domains (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    domain_name     VARCHAR(255) NOT NULL,
    status          domain_status NOT NULL DEFAULT 'pending_verification',
    ses_identity_arn VARCHAR(512),                       -- ARN returned by SES VerifyDomainIdentity
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    verified_at     TIMESTAMPTZ,
    last_checked_at TIMESTAMPTZ,
    CONSTRAINT uq_domains_tenant_name UNIQUE (tenant_id, domain_name)
);

CREATE INDEX idx_domains_tenant ON domains(tenant_id);
CREATE INDEX idx_domains_status ON domains(tenant_id, status);

-- -----------------------------------------------------------
-- 3.4 dns_records
-- Individual DNS records for domain verification.
-- -----------------------------------------------------------
CREATE TABLE dns_records (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    domain_id       UUID NOT NULL REFERENCES domains(id) ON DELETE CASCADE,
    record_type     VARCHAR(10) NOT NULL,                -- TXT, CNAME, MX
    record_name     VARCHAR(512) NOT NULL,               -- Full DNS name
    record_value    VARCHAR(1024) NOT NULL,              -- Expected value
    purpose         dns_record_purpose NOT NULL,          -- spf, dkim, dmarc
    is_verified     BOOLEAN NOT NULL DEFAULT FALSE,
    verified_at     TIMESTAMPTZ,
    actual_value    VARCHAR(1024),                       -- Value found during verification (for debugging)
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()   -- Tracks when verification status last changed
);

CREATE INDEX idx_dns_records_domain ON dns_records(domain_id);

-- -----------------------------------------------------------
-- 3.5 templates
-- Email templates with Liquid syntax. Soft-delete support.
-- -----------------------------------------------------------
CREATE TABLE templates (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    name            VARCHAR(255) NOT NULL,
    subject_template VARCHAR(1024) NOT NULL,
    html_body       TEXT NOT NULL,
    text_body       TEXT,
    variables_schema JSONB,                              -- JSON Schema defining expected variables
    version         INT NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,                         -- Soft delete
    CONSTRAINT uq_templates_tenant_name UNIQUE (tenant_id, name) WHERE deleted_at IS NULL
);

-- Partial unique index: name must be unique among non-deleted templates per tenant
-- The CONSTRAINT above won't work in PostgreSQL. Use a partial unique index instead:
DROP INDEX IF EXISTS uq_templates_tenant_name;
CREATE UNIQUE INDEX uq_templates_tenant_name_active
    ON templates(tenant_id, name)
    WHERE deleted_at IS NULL;

CREATE INDEX idx_templates_tenant ON templates(tenant_id);
CREATE INDEX idx_templates_deleted ON templates(tenant_id, deleted_at) WHERE deleted_at IS NULL;

-- -----------------------------------------------------------
-- 3.6 emails
-- Core email records. Regular table for Sprint 1.
-- Partitioning deferred to a future sprint when volume justifies it.
-- -----------------------------------------------------------
CREATE TABLE emails (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    api_key_id      UUID NOT NULL REFERENCES api_keys(id),
    message_id      VARCHAR(32) NOT NULL,                -- msg_ + 12 alphanumeric
    batch_id        VARCHAR(32),                         -- batch_ + 8 alphanumeric (null for single sends)
    from_email      VARCHAR(320) NOT NULL,
    from_name       VARCHAR(255),
    to_emails       JSONB NOT NULL,                      -- Array of { email, name }
    cc_emails       JSONB DEFAULT '[]',
    bcc_emails      JSONB DEFAULT '[]',
    subject         VARCHAR(998) NOT NULL,               -- RFC 2822 max subject length
    html_body       TEXT,
    text_body       TEXT,
    template_id     UUID,
    variables       JSONB,
    attachments     JSONB DEFAULT '[]',                  -- Array of { filename, content_type, size_bytes } -- content not stored in DB
    tags            TEXT[] DEFAULT '{}',
    metadata        JSONB DEFAULT '{}',
    track_opens     BOOLEAN NOT NULL DEFAULT TRUE,
    track_clicks    BOOLEAN NOT NULL DEFAULT TRUE,
    status          email_status NOT NULL DEFAULT 'queued',
    ses_message_id  VARCHAR(255),                        -- SES Message-ID for correlation
    error_message   TEXT,                                -- Error detail if status = failed
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    sent_at         TIMESTAMPTZ,
    delivered_at    TIMESTAMPTZ,
    opened_at       TIMESTAMPTZ,                         -- First open
    clicked_at      TIMESTAMPTZ                          -- First click
);

CREATE UNIQUE INDEX idx_emails_message_id ON emails(message_id);
CREATE INDEX idx_emails_tenant_status ON emails(tenant_id, status, created_at DESC);
CREATE INDEX idx_emails_tenant_created ON emails(tenant_id, created_at DESC);
CREATE INDEX idx_emails_batch ON emails(batch_id) WHERE batch_id IS NOT NULL;
CREATE INDEX idx_emails_template ON emails(template_id) WHERE template_id IS NOT NULL;
CREATE INDEX idx_emails_api_key ON emails(api_key_id);
CREATE INDEX idx_emails_from ON emails(from_email);

-- -----------------------------------------------------------
-- 3.7 email_events
-- Lifecycle events for each email. Append-only.
-- Regular table for Sprint 1. Partitioning deferred.
-- -----------------------------------------------------------
CREATE TABLE email_events (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email_id        UUID NOT NULL REFERENCES emails(id),
    event_type      event_type NOT NULL,
    data            JSONB DEFAULT '{}',                  -- Event-specific payload (bounce details, click URL, etc.)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_email_events_email ON email_events(email_id);
CREATE INDEX idx_email_events_type ON email_events(event_type, created_at);

-- -----------------------------------------------------------
-- 3.8 suppression_list
-- Email addresses that must not receive mail.
-- -----------------------------------------------------------
CREATE TABLE suppression_list (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    email_address   VARCHAR(320) NOT NULL,               -- RFC 5321 max email length
    reason          suppression_reason NOT NULL,
    source_message_id VARCHAR(32),                       -- The email that caused the suppression
    suppressed_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_suppression_tenant_email UNIQUE (tenant_id, email_address)
);

CREATE INDEX idx_suppression_tenant ON suppression_list(tenant_id);
CREATE INDEX idx_suppression_email ON suppression_list(email_address);

-- -----------------------------------------------------------
-- 3.9 webhooks
-- Webhook endpoint configurations (P2, but table created now).
-- -----------------------------------------------------------
CREATE TABLE webhooks (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    url             VARCHAR(2048) NOT NULL,
    events          TEXT[] NOT NULL,                      -- Array of event type strings
    secret          VARCHAR(255),                         -- HMAC secret for signature
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_webhooks_tenant ON webhooks(tenant_id);

-- -----------------------------------------------------------
-- 3.10 dashboard_users
-- Single-user auth for Sprint 1 dashboard.
-- -----------------------------------------------------------
CREATE TABLE dashboard_users (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username        VARCHAR(100) NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,               -- bcrypt hash
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## 4. API Specification

**Base URL:** `https://email.israeliyonsi.dev/api/v1`
**Authentication:** All endpoints (except `/health`) require `Authorization: Bearer {api_key}` header.
**Rate Limit:** 100 requests/second per API key (429 Too Many Requests when exceeded).
**Content-Type:** `application/json` for all requests and responses.

### Common Response Envelope

```json
// Success
{ "success": true, "data": { ... } }

// Error
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Human-readable message",
    "details": [{ "field": "to", "message": "Required field" }]
  }
}
```

### Common Error Codes

| Code | HTTP Status | Meaning |
|------|-------------|---------|
| `VALIDATION_ERROR` | 400 | Request body validation failed |
| `UNAUTHORIZED` | 401 | Missing or invalid API key |
| `FORBIDDEN` | 403 | API key not permitted for this domain |
| `NOT_FOUND` | 404 | Resource not found |
| `CONFLICT` | 409 | Resource already exists |
| `RECIPIENT_SUPPRESSED` | 422 | Recipient is on the suppression list |
| `DOMAIN_NOT_VERIFIED` | 422 | From-domain is not verified |
| `RATE_LIMITED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

---

### 4.1 POST /api/v1/emails/send

Send a single transactional email.

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `from` | `object` | Yes | `{ "email": string, "name": string? }` |
| `to` | `object[]` | Yes | `[{ "email": string, "name": string? }]` -- max 50 |
| `cc` | `object[]` | No | Same format as `to` |
| `bcc` | `object[]` | No | Same format as `to` |
| `subject` | `string` | Conditional | Required if no `template_id` |
| `html_body` | `string` | Conditional | Required if no `template_id` |
| `text_body` | `string` | No | Plain text fallback |
| `template_id` | `string` | No | Template ID to render. Mutually exclusive with `html_body` |
| `variables` | `object` | No | Template variables. Required if `template_id` is set |
| `attachments` | `object[]` | No | `[{ "filename": string, "content": string (base64), "content_type": string }]` |
| `tags` | `string[]` | No | Arbitrary tags for filtering |
| `metadata` | `object` | No | Arbitrary key-value metadata |
| `track_opens` | `boolean` | No | Default: `true` |
| `track_clicks` | `boolean` | No | Default: `true` |

**Validation Rules:**
- `from.email` must be a valid email on a verified domain
- `to` must have at least 1 item, max 50
- Combined `to` + `cc` + `bcc` must not exceed 50
- Individual attachment max: 10MB. Total attachments max: 25MB
- Either (`subject` + `html_body`) or `template_id` must be provided, not both
- All email addresses validated for RFC 5322 format

**Response: 202 Accepted**

```json
{
  "success": true,
  "data": {
    "message_id": "msg_a1b2c3d4e5f6",
    "status": "queued",
    "queued_at": "2026-03-27T10:30:00Z"
  }
}
```

**Error Responses:**
- `400` -- Validation error (invalid fields)
- `401` -- Invalid API key
- `403` -- API key not authorized for this from-domain
- `404` -- Template not found (if `template_id` provided)
- `422` -- Recipient suppressed or domain not verified
- `429` -- Rate limited

---

### 4.2 POST /api/v1/emails/batch

Send up to 100 emails in a single request.

**Request Body:**

```json
{
  "emails": [
    { /* Same schema as single send */ }
  ]
}
```

- `emails` array: min 1, max 100 items
- Each item validated independently

**Response: 202 Accepted**

```json
{
  "success": true,
  "data": {
    "batch_id": "batch_m3n4o5p6",
    "total": 2,
    "accepted": 2,
    "rejected": 0,
    "messages": [
      { "index": 0, "message_id": "msg_q7r8s9t0", "status": "queued" },
      { "index": 1, "message_id": "msg_u1v2w3x4", "status": "queued" }
    ]
  }
}
```

If some items fail validation, the response still returns 202 but marks rejected items:

```json
{
  "success": true,
  "data": {
    "batch_id": "batch_m3n4o5p6",
    "total": 3,
    "accepted": 2,
    "rejected": 1,
    "messages": [
      { "index": 0, "message_id": "msg_q7r8s9t0", "status": "queued" },
      { "index": 1, "message_id": null, "status": "rejected", "error": "Recipient suppressed: bad@example.com" },
      { "index": 2, "message_id": "msg_u1v2w3x4", "status": "queued" }
    ]
  }
}
```

---

### 4.3 GET /api/v1/emails/{id}

Get email details by message ID.

**Path Parameters:** `id` -- the `message_id` (e.g., `msg_a1b2c3d4e5f6`)

**Response: 200 OK**

```json
{
  "success": true,
  "data": {
    "message_id": "msg_a1b2c3d4e5f6",
    "from": { "email": "invoices@cashtrack.app", "name": "CashTrack" },
    "to": [{ "email": "client@example.com", "name": "John Doe" }],
    "cc": [],
    "bcc": [],
    "subject": "Invoice #INV-2026-001",
    "template_id": null,
    "tags": ["invoice", "cashtrack"],
    "metadata": { "invoice_id": "INV-2026-001" },
    "status": "delivered",
    "created_at": "2026-03-27T10:30:00Z",
    "sent_at": "2026-03-27T10:30:02Z",
    "delivered_at": "2026-03-27T10:30:05Z",
    "opened_at": "2026-03-27T11:15:00Z",
    "clicked_at": null,
    "events": [
      { "type": "queued", "timestamp": "2026-03-27T10:30:00Z", "data": {} },
      { "type": "sent", "timestamp": "2026-03-27T10:30:02Z", "data": { "ses_message_id": "..." } },
      { "type": "delivered", "timestamp": "2026-03-27T10:30:05Z", "data": {} },
      { "type": "opened", "timestamp": "2026-03-27T11:15:00Z", "data": { "ip": "...", "user_agent": "..." } }
    ]
  }
}
```

**Error Responses:** `404` -- Email not found

---

### 4.4 GET /api/v1/emails

List emails with filtering and pagination.

**Query Parameters:**

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `page_size` | int | 50 | Items per page (max 200) |
| `status` | string | -- | Filter by status |
| `from` | string | -- | Filter by from email |
| `to` | string | -- | Filter by recipient email |
| `template_id` | string | -- | Filter by template |
| `api_key_id` | string | -- | Filter by API key |
| `start_date` | ISO 8601 | -- | Start of date range |
| `end_date` | ISO 8601 | -- | End of date range |
| `sort_by` | string | `created_at` | Sort field |
| `sort_dir` | string | `desc` | `asc` or `desc` |

**Response: 200 OK**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "message_id": "msg_a1b2c3d4e5f6",
        "from": "invoices@cashtrack.app",
        "to": "client@example.com",
        "subject": "Invoice #INV-2026-001",
        "status": "delivered",
        "template_id": null,
        "created_at": "2026-03-27T10:30:00Z",
        "delivered_at": "2026-03-27T10:30:05Z"
      }
    ],
    "total": 1250,
    "page": 1,
    "page_size": 50,
    "total_pages": 25
  }
}
```

---

### 4.5 POST /api/v1/templates

Create a new email template.

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Unique template name (max 255 chars) |
| `subject_template` | string | Yes | Liquid-syntax subject line (max 1024 chars) |
| `html_body` | string | Yes | Liquid HTML template (max 512KB) |
| `text_body` | string | No | Liquid plain text template |
| `variables_schema` | object | No | JSON Schema for expected variables |

**Validation Rules:**
- `name` must be unique among active (non-deleted) templates
- `html_body` + `text_body` combined must not exceed 512KB
- Liquid syntax is validated; syntax errors return 400
- `variables_schema` if provided must be valid JSON Schema

**Response: 201 Created**

```json
{
  "success": true,
  "data": {
    "template_id": "tmpl_a1b2c3d4",
    "name": "payment_reminder",
    "version": 1,
    "created_at": "2026-03-27T10:00:00Z"
  }
}
```

**Error Responses:**
- `400` -- Validation or Liquid syntax error
- `409` -- Template name already exists

---

### 4.6 GET /api/v1/templates

List templates with pagination.

**Query Parameters:**

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `page_size` | int | 20 | Items per page (max 100) |
| `name` | string | -- | Partial name match filter |
| `sort_by` | string | `created_at` | `created_at` or `updated_at` |
| `sort_dir` | string | `desc` | `asc` or `desc` |

**Response: 200 OK**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "template_id": "tmpl_a1b2c3d4",
        "name": "payment_reminder",
        "version": 2,
        "created_at": "2026-03-27T10:00:00Z",
        "updated_at": "2026-03-27T12:00:00Z"
      }
    ],
    "total": 5,
    "page": 1,
    "page_size": 20,
    "total_pages": 1
  }
}
```

---

### 4.7 GET /api/v1/templates/{id}

Get full template details.

**Response: 200 OK**

```json
{
  "success": true,
  "data": {
    "template_id": "tmpl_a1b2c3d4",
    "name": "payment_reminder",
    "subject_template": "Payment Reminder: {{ invoice_number }}",
    "html_body": "<!DOCTYPE html>...",
    "text_body": "Hi {{ client_name }}...",
    "variables_schema": { "type": "object", "required": ["client_name"], "properties": { ... } },
    "version": 2,
    "created_at": "2026-03-27T10:00:00Z",
    "updated_at": "2026-03-27T12:00:00Z"
  }
}
```

---

### 4.8 PUT /api/v1/templates/{id}

Update an existing template.

**Request Body:** Same as POST (all fields optional, only provided fields are updated).

**Behavior:**
- Increments `version` by 1
- Updates `updated_at`
- Invalidates Redis cache for this template
- Previous content is updated in place with version counter only. No `template_versions` table in Sprint 1 -- full version history is deferred to Sprint 2

**Response: 200 OK** -- returns updated template (same as GET)

---

### 4.9 DELETE /api/v1/templates/{id}

Soft-delete a template.

**Behavior:**
- Sets `deleted_at` to current timestamp
- Removes from Redis cache
- Excluded from list results
- Sends referencing this template_id will return 404

**Response: 204 No Content**

---

### 4.10 POST /api/v1/domains

Add a sending domain.

**Request Body:**

```json
{ "domain": "notifications.cashtrack.app" }
```

**Validation:**
- Valid domain format (RFC 1035)
- Not a duplicate within the tenant

**Behavior:**
1. Calls SES `VerifyDomainIdentity` API to initiate verification
2. Calls SES `VerifyDomainDkim` API to get DKIM tokens
3. Generates DNS records (SPF TXT, 3x DKIM CNAME, DMARC TXT)
4. Saves domain and DNS records to database
5. Returns DNS records the user must configure

**Response: 201 Created**

```json
{
  "success": true,
  "data": {
    "domain_id": "dom_x1y2z3",
    "domain": "notifications.cashtrack.app",
    "status": "pending_verification",
    "dns_records": [
      { "type": "TXT", "name": "notifications.cashtrack.app", "value": "v=spf1 include:amazonses.com ~all", "purpose": "spf", "is_verified": false },
      { "type": "CNAME", "name": "abcdef._domainkey.notifications.cashtrack.app", "value": "abcdef.dkim.amazonses.com", "purpose": "dkim", "is_verified": false },
      { "type": "CNAME", "name": "ghijkl._domainkey.notifications.cashtrack.app", "value": "ghijkl.dkim.amazonses.com", "purpose": "dkim", "is_verified": false },
      { "type": "CNAME", "name": "mnopqr._domainkey.notifications.cashtrack.app", "value": "mnopqr.dkim.amazonses.com", "purpose": "dkim", "is_verified": false },
      { "type": "TXT", "name": "_dmarc.notifications.cashtrack.app", "value": "v=DMARC1; p=quarantine; rua=mailto:dmarc@israeliyonsi.dev", "purpose": "dmarc", "is_verified": false }
    ],
    "created_at": "2026-03-27T10:00:00Z"
  }
}
```

**Error Responses:**
- `400` -- Invalid domain format
- `409` -- Domain already registered

---

### 4.11 GET /api/v1/domains

List all domains.

**Response: 200 OK**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "domain_id": "dom_x1y2z3",
        "domain": "notifications.cashtrack.app",
        "status": "verified",
        "verified_at": "2026-03-27T12:00:00Z",
        "last_checked_at": "2026-03-28T00:00:00Z",
        "dns_records": [ ... ]
      }
    ]
  }
}
```

---

### 4.12 POST /api/v1/domains/{id}/verify

Trigger DNS verification for a domain.

**Behavior:**
1. Fetches expected DNS records from database
2. Queries actual DNS records using DnsClient.NET
3. Compares expected vs actual for each record
4. Updates `is_verified` per record
5. If all records verified: sets domain status to `verified`
6. If any record fails: sets domain status to `failed`

**Response: 200 OK**

```json
{
  "success": true,
  "data": {
    "domain_id": "dom_x1y2z3",
    "domain": "notifications.cashtrack.app",
    "status": "verified",
    "dns_records": [
      { "type": "TXT", "name": "notifications.cashtrack.app", "purpose": "spf", "is_verified": true, "expected": "v=spf1 include:amazonses.com ~all", "actual": "v=spf1 include:amazonses.com ~all" },
      { "type": "CNAME", "name": "abcdef._domainkey.notifications.cashtrack.app", "purpose": "dkim", "is_verified": true, "expected": "abcdef.dkim.amazonses.com", "actual": "abcdef.dkim.amazonses.com" }
    ],
    "verified_at": "2026-03-27T12:00:00Z"
  }
}
```

---

### 4.13 POST /api/v1/keys

Create an API key.

**Request Body:**

```json
{
  "name": "CashTrack Production",
  "allowed_domains": ["notifications.cashtrack.app"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Descriptive name (max 255 chars) |
| `allowed_domains` | string[] | No | Restrict to specific sending domains. Empty = all domains. |

**Behavior:**
1. Generates API key: `eaas_live_` + 40 random alphanumeric characters
2. Stores SHA-256 hash of the full key
3. Stores first 8 characters as `prefix` for identification
4. Returns the full key -- this is the only time it is returned

**Response: 201 Created**

```json
{
  "success": true,
  "data": {
    "key_id": "key_m1n2o3p4",
    "name": "CashTrack Production",
    "api_key": "eaas_live_aBcDeFgH1234567890xYzWvUtSrQpOnMlKjIhG",
    "prefix": "eaas_liv",
    "allowed_domains": ["notifications.cashtrack.app"],
    "created_at": "2026-03-27T10:00:00Z"
  }
}
```

---

### 4.14 GET /api/v1/keys

List all API keys (without the secret key value).

**Response: 200 OK**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "key_id": "key_m1n2o3p4",
        "name": "CashTrack Production",
        "prefix": "eaas_liv",
        "allowed_domains": ["notifications.cashtrack.app"],
        "status": "active",
        "created_at": "2026-03-27T10:00:00Z",
        "last_used_at": "2026-03-27T15:00:00Z",
        "send_count": 342
      }
    ]
  }
}
```

---

### 4.15 DELETE /api/v1/keys/{id}

Revoke an API key.

**Behavior:**
- Sets `status` = `revoked`, `revoked_at` = now
- Subsequent requests with this key return 401
- Emails already queued continue processing

**Response: 204 No Content**

---

### 4.16 GET /health

Health check endpoint. No authentication required.

**Response: 200 OK** (all healthy) or **503 Service Unavailable** (any component unhealthy)

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "uptime_seconds": 864000,
  "components": {
    "api": { "status": "healthy" },
    "database": { "status": "healthy", "latency_ms": 2 },
    "redis": { "status": "healthy", "latency_ms": 1 },
    "rabbitmq": { "status": "healthy", "queue_depth": 12 },
    "ses": { "status": "healthy", "daily_quota_remaining": 49500 }
  }
}
```

---

## 5. Message Queue Design

### 5.1 Exchange and Queue Topology

**Why RabbitMQ over a simpler queue:** RabbitMQ provides dead-letter exchanges, message TTLs for delayed retry, per-message persistence, and consumer acknowledgment -- all critical for reliable email delivery. Redis Streams would work but lacks DLX routing.

**Why classic durable queues (not quorum):** Quorum queues use Raft consensus for replication, which provides no benefit on a single-node RabbitMQ instance and consumes more memory. Classic durable queues with `durable: true` and persistent messages survive broker restarts, which is all that is needed for Sprint 1.

**MassTransit handles queue topology automatically.** The exchange/queue bindings, retry queues, and DLQ routing described below are declared by MassTransit on bus startup. The developer does not need to manually create any exchanges or queues.

```
-- MassTransit auto-declares this topology on startup --

Exchange: eaas.direct (type: direct, durable: true)
├── Routing Key: email.send
│   └── Queue: eaas.emails.send (classic, durable)
│       ├── x-dead-letter-exchange: eaas.dlx
│       ├── x-dead-letter-routing-key: email.send.failed
│       └── x-message-ttl: 300000 (5 min max age before DLQ, as safety net)
│
├── Routing Key: webhook.deliver
│   └── Queue: eaas.webhooks.deliver (classic, durable)
│       ├── x-dead-letter-exchange: eaas.dlx
│       └── x-dead-letter-routing-key: webhook.deliver.failed

Exchange: eaas.dlx (type: direct, durable: true)
├── Routing Key: email.send.failed
│   └── Queue: eaas.emails.send.dlq (classic, durable)
│
├── Routing Key: webhook.deliver.failed
│   └── Queue: eaas.webhooks.deliver.dlq (classic, durable)
```

### 5.2 Retry Strategy (via MassTransit)

MassTransit handles retries declaratively via `UseMessageRetry` and `UseDelayedRedelivery`. No manual retry queues needed.

| Attempt | Delay | Mechanism |
|---------|-------|-----------|
| 1st retry | 1 minute | MassTransit `UseDelayedRedelivery` |
| 2nd retry | 5 minutes | MassTransit `UseDelayedRedelivery` |
| 3rd retry | 30 minutes | MassTransit `UseDelayedRedelivery` |
| After 3rd | -- | Moved to `_error` queue (DLQ) |

**MassTransit configuration (in `MassTransitConfiguration.cs`):**

```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<SendEmailConsumer>();
    x.AddConsumer<WebhookDeliveryConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/eaas", h =>
        {
            h.Username(config["RabbitMq:Username"]);
            h.Password(config["RabbitMq:Password"]);
        });

        cfg.UseDelayedRedelivery(r => r.Intervals(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30)
        ));

        cfg.UseMessageRetry(r => r.Immediate(1));  // Immediate retry for transient failures

        cfg.ConfigureEndpoints(context);
    });
});
```

**Retry tracking:** MassTransit tracks retry count via message headers. After all retries are exhausted, the message is moved to the `_error` queue automatically.

### 5.3 Message Schema

#### SendEmailMessage

Published to `eaas.direct` with routing key `email.send`.

```json
{
  "email_id": "uuid",
  "message_id": "msg_a1b2c3d4e5f6",
  "tenant_id": "uuid",
  "api_key_id": "uuid",
  "from": { "email": "invoices@cashtrack.app", "name": "CashTrack" },
  "to": [{ "email": "client@example.com", "name": "John Doe" }],
  "cc": [],
  "bcc": [],
  "subject": "Invoice #INV-2026-001",
  "html_body": "<h1>Your Invoice</h1>...",
  "text_body": "Your Invoice...",
  "template_id": null,
  "variables": null,
  "attachments": [
    {
      "filename": "invoice.pdf",
      "content": "base64...",
      "content_type": "application/pdf"
    }
  ],
  "track_opens": true,
  "track_clicks": true,
  "enqueued_at": "2026-03-27T10:30:00Z"
}
```

**Why include the full body in the message:** Avoids a DB roundtrip in the worker for every email. The message is self-contained. At 100KB average message size and low MVP volume, this is well within limits. For Sprint 2 when attachments are added, the message schema must change to store attachment content elsewhere and pass only a reference.

#### ProcessWebhookMessage

Published to `eaas.direct` with routing key `webhook.deliver`.

```json
{
  "webhook_id": "uuid",
  "url": "https://cashtrack.app/webhooks/eaas",
  "secret": "whsec_...",
  "event": "email.delivered",
  "payload": {
    "message_id": "msg_a1b2c3d4e5f6",
    "timestamp": "2026-03-27T10:30:05Z",
    "data": { ... }
  }
}
```

### 5.4 Consumer Configuration (MassTransit)

| Setting | Value | Why |
|---------|-------|-----|
| Prefetch count | 10 | Balance throughput vs memory. SES API calls take ~100ms; 10 in-flight keeps the pipeline full without overwhelming the VPS. Configured via `cfg.PrefetchCount = 10;` |
| Concurrency limit | 10 | MassTransit manages ack/nack automatically. `UseConcurrencyLimit(10)` controls parallelism per consumer. |
| Message persistence | Durable queues + persistent delivery | MassTransit defaults to durable queues and persistent messages. Survives RabbitMQ restart. |
| Error handling | Automatic | MassTransit moves failed messages (after retry exhaustion) to `_error` queues. No manual DLQ routing needed. |

---

## 6. AWS SES Integration Design

### 6.1 Configuration

```
AWS_REGION=eu-west-1                       # Ireland -- closest to Hetzner Germany
AWS_ACCESS_KEY_ID=AKIA...                  # IAM user with SES-only permissions
AWS_SECRET_ACCESS_KEY=...                  # IAM user secret
```

**IAM Policy (least privilege):**

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail",
        "ses:GetSendQuota",
        "ses:GetSendStatistics",
        "ses:VerifyDomainIdentity",
        "ses:VerifyDomainDkim",
        "ses:DeleteIdentity",
        "ses:GetIdentityVerificationAttributes",
        "ses:GetIdentityDkimAttributes"
      ],
      "Resource": "*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "sns:Subscribe",
        "sns:ConfirmSubscription"
      ],
      "Resource": "arn:aws:sns:eu-west-1:*:eaas-*"
    }
  ]
}
```

**Why both `ses:SendEmail` and `ses:SendRawEmail` in the IAM policy:** The SES v2 SDK (`AWSSDK.SimpleEmailV2`) uses a unified `SendEmail` API action that supports both simple and raw content. The v1 action `ses:SendRawEmail` is included for backward compatibility. Both must be present to avoid permissions errors regardless of which SDK method is used.

### 6.2 Domain Verification Flow

```
1. User calls POST /api/v1/domains with { "domain": "notifications.cashtrack.app" }
2. API calls SES VerifyDomainIdentity("notifications.cashtrack.app")
   -> Returns a verification token (TXT record value)
3. API calls SES VerifyDomainDkim("notifications.cashtrack.app")
   -> Returns 3 DKIM tokens
4. API constructs DNS records:
   a. SPF:  TXT  notifications.cashtrack.app          "v=spf1 include:amazonses.com ~all"
   b. DKIM: CNAME {token1}._domainkey.notifications... -> {token1}.dkim.amazonses.com
   c. DKIM: CNAME {token2}._domainkey.notifications... -> {token2}.dkim.amazonses.com
   d. DKIM: CNAME {token3}._domainkey.notifications... -> {token3}.dkim.amazonses.com
   e. DMARC: TXT  _dmarc.notifications.cashtrack.app  "v=DMARC1; p=quarantine; rua=mailto:dmarc@israeliyonsi.dev"
5. DNS records returned to user. User configures them in their DNS provider.
6. User calls POST /api/v1/domains/{id}/verify
7. API uses DnsClient.NET to query each record
8. API compares expected vs actual values
9. If all pass: domain status = verified; user can now send from this domain
```

### 6.3 DKIM/SPF/DMARC Setup

**SPF:** The TXT record tells receiving mail servers that `amazonses.com` is authorized to send on behalf of the domain. The `~all` softfail means other senders are not explicitly rejected but marked suspicious.

**DKIM:** SES signs outbound emails with a domain-specific key. The 3 CNAME records point to SES-hosted public keys. Receiving servers verify the signature against these keys.

**DMARC:** Tells receiving servers what to do when SPF or DKIM checks fail. `p=quarantine` means move to spam rather than reject outright. `rua=mailto:...` receives aggregate reports for monitoring.

### 6.4 SNS Topic Configuration for Bounces/Complaints

**Setup (one-time, done during infrastructure provisioning):**

1. Create SNS topic: `eaas-ses-notifications` in `eu-west-1`
2. Create HTTPS subscription pointing to: `https://email.israeliyonsi.dev/webhooks/sns`
3. Configure SES notification types for each verified identity:
   - Bounce notifications -> `eaas-ses-notifications`
   - Complaint notifications -> `eaas-ses-notifications`
   - Delivery notifications -> `eaas-ses-notifications`
4. Disable email notifications (we use SNS exclusively)

**Why a single topic for all event types:** Simplifies configuration. The webhook processor inspects the `notificationType` field in the SNS message body to route to the appropriate handler.

### 6.5 Webhook Processing Flow

```
1. SES sends email
2. Receiving mail server responds (delivered / bounced / complaint)
3. SES publishes notification to SNS topic
4. SNS sends HTTPS POST to https://email.israeliyonsi.dev/webhooks/sns
5. WebhookProcessor receives the POST
6. SnsMessageValidator:
   a. Checks SignatureVersion == "1"
   b. Downloads signing certificate from SigningCertURL (must be *.amazonaws.com)
   c. Verifies SHA1WithRSA signature against message content
   d. If Type == "SubscriptionConfirmation": auto-confirms by GETting the SubscribeURL
   e. If Type == "Notification": parses the Message body
7. Routes to handler based on notificationType:
   a. "Bounce" -> BounceProcessor
      - Extracts bounced recipients
      - For hard bounces: adds each to suppression_list, updates email status to 'bounced'
      - For soft bounces: increments retry counter; after 3 consecutive soft bounces for same address -> suppress
   b. "Complaint" -> ComplaintProcessor
      - Adds complained address to suppression_list
      - Updates email status to 'complained'
      - Logs complaint feedback type
   c. "Delivery" -> DeliveryProcessor
      - Updates email status to 'delivered'
      - Sets delivered_at timestamp
8. Each processor also caches the suppression in Redis for O(1) lookup at send time
```

---

## 7. Docker Compose

```yaml
# docker-compose.yml
# EaaS - Email as a Service
# All services on a single Hetzner CX22 VPS (2 vCPU, 4GB RAM, 40GB SSD)

version: "3.9"

services:
  # ============================================================
  # INFRASTRUCTURE
  # ============================================================

  postgres:
    image: postgres:16.2-alpine
    container_name: eaas-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: eaas
      POSTGRES_USER: eaas_app
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
    ports:
      - "127.0.0.1:5432:5432"              # Bind to localhost only -- no external access
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U eaas_app -d eaas"]
      interval: 10s
      timeout: 5s
      retries: 5
    deploy:
      resources:
        limits:
          memory: 512M

  redis:
    image: redis:7.2-alpine
    container_name: eaas-redis
    restart: unless-stopped
    command: >
      redis-server
      --maxmemory 128mb
      --maxmemory-policy allkeys-lru
      --appendonly yes
      --requirepass ${REDIS_PASSWORD}
    volumes:
      - redis_data:/data
    ports:
      - "127.0.0.1:6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    deploy:
      resources:
        limits:
          memory: 192M

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    container_name: eaas-rabbitmq
    restart: unless-stopped
    environment:
      RABBITMQ_DEFAULT_USER: eaas_app
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
      RABBITMQ_DEFAULT_VHOST: eaas
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    ports:
      - "127.0.0.1:5672:5672"              # AMQP
      - "127.0.0.1:15672:15672"            # Management UI
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 15s
      timeout: 10s
      retries: 5
    deploy:
      resources:
        limits:
          memory: 512M

  # ============================================================
  # APPLICATION SERVICES
  # ============================================================

  api:
    build:
      context: .
      dockerfile: src/EaaS.Api/Dockerfile
    container_name: eaas-api
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__PostgreSQL=Host=postgres;Port=5432;Database=eaas;Username=eaas_app;Password=${POSTGRES_PASSWORD}
      - ConnectionStrings__Redis=redis:6379,password=${REDIS_PASSWORD}
      - RabbitMq__Host=rabbitmq
      - RabbitMq__Port=5672
      - RabbitMq__Username=eaas_app
      - RabbitMq__Password=${RABBITMQ_PASSWORD}
      - RabbitMq__VirtualHost=eaas
      - Ses__Region=${AWS_REGION}
      - Ses__AccessKeyId=${AWS_ACCESS_KEY_ID}
      - Ses__SecretAccessKey=${AWS_SECRET_ACCESS_KEY}
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "wget", "-q", "--spider", "http://localhost:8080/health"]
      interval: 15s
      timeout: 5s
      retries: 3
    deploy:
      resources:
        limits:
          memory: 256M

  worker:
    build:
      context: .
      dockerfile: src/EaaS.Worker/Dockerfile
    container_name: eaas-worker
    restart: unless-stopped
    environment:
      - DOTNET_ENVIRONMENT=Production
      - ConnectionStrings__PostgreSQL=Host=postgres;Port=5432;Database=eaas;Username=eaas_app;Password=${POSTGRES_PASSWORD}
      - ConnectionStrings__Redis=redis:6379,password=${REDIS_PASSWORD}
      - RabbitMq__Host=rabbitmq
      - RabbitMq__Port=5672
      - RabbitMq__Username=eaas_app
      - RabbitMq__Password=${RABBITMQ_PASSWORD}
      - RabbitMq__VirtualHost=eaas
      - Ses__Region=${AWS_REGION}
      - Ses__AccessKeyId=${AWS_ACCESS_KEY_ID}
      - Ses__SecretAccessKey=${AWS_SECRET_ACCESS_KEY}
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    deploy:
      resources:
        limits:
          memory: 256M

  webhook-processor:
    build:
      context: .
      dockerfile: src/EaaS.WebhookProcessor/Dockerfile
    container_name: eaas-webhook-processor
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8081
      - ConnectionStrings__PostgreSQL=Host=postgres;Port=5432;Database=eaas;Username=eaas_app;Password=${POSTGRES_PASSWORD}
      - ConnectionStrings__Redis=redis:6379,password=${REDIS_PASSWORD}
      - Tracking__BaseUrl=https://email.israeliyonsi.dev
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    deploy:
      resources:
        limits:
          memory: 128M

  dashboard:
    build:
      context: .
      context: ./dashboard
      dockerfile: Dockerfile
    container_name: eaas-dashboard
    restart: unless-stopped
    environment:
      - NODE_ENV=production
      - NEXT_PUBLIC_API_URL=http://api:8080
    depends_on:
      api:
        condition: service_healthy
    deploy:
      resources:
        limits:
          memory: 192M

  # ============================================================
  # REVERSE PROXY
  # ============================================================

  nginx:
    image: nginx:1.25-alpine
    container_name: eaas-nginx
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - certbot_data:/var/www/certbot:ro
    depends_on:
      - api
      - webhook-processor
      - dashboard
    deploy:
      resources:
        limits:
          memory: 64M

  # ============================================================
  # TLS CERTIFICATE MANAGEMENT
  # ============================================================

  certbot:
    image: certbot/certbot:v2.9.0
    container_name: eaas-certbot
    volumes:
      - ./nginx/ssl:/etc/letsencrypt
      - certbot_data:/var/www/certbot
    entrypoint: "/bin/sh -c 'trap exit TERM; while :; do certbot renew; sleep 12h & wait $${!}; done;'"

volumes:
  postgres_data:
  redis_data:
  rabbitmq_data:
  certbot_data:
```

### Nginx Configuration

```nginx
# nginx/nginx.conf

worker_processes auto;
events { worker_connections 1024; }

http {
    # Security headers
    add_header X-Frame-Options DENY always;
    add_header X-Content-Type-Options nosniff always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # Rate limiting zone
    limit_req_zone $binary_remote_addr zone=api:10m rate=100r/s;

    # Gzip
    gzip on;
    gzip_types application/json text/plain text/html;

    # Redirect HTTP to HTTPS
    server {
        listen 80;
        server_name email.israeliyonsi.dev;

        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
        }

        location / {
            return 301 https://$host$request_uri;
        }
    }

    # HTTPS server
    server {
        listen 443 ssl http2;
        server_name email.israeliyonsi.dev;

        ssl_certificate /etc/nginx/ssl/live/email.israeliyonsi.dev/fullchain.pem;
        ssl_certificate_key /etc/nginx/ssl/live/email.israeliyonsi.dev/privkey.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers HIGH:!aNULL:!MD5;
        ssl_prefer_server_ciphers on;

        client_max_body_size 30M;          # 25MB attachments + overhead

        # API endpoints
        location /api/ {
            limit_req zone=api burst=20 nodelay;
            proxy_pass http://api:8080;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # Health check (no rate limit)
        location /health {
            proxy_pass http://api:8080;
            proxy_set_header Host $host;
        }

        # SNS webhooks (from AWS -- no rate limit, IP-restricted in production)
        location /webhooks/sns {
            proxy_pass http://webhook-processor:8081;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # Tracking endpoints (opens/clicks)
        location /track/ {
            proxy_pass http://webhook-processor:8081;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
        }

        # Dashboard
        location / {
            proxy_pass http://dashboard:8082;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            # Next.js dashboard proxy
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
        }
    }
}
```

---

## 8. Configuration Design

### 8.1 Environment Variables

All secrets are passed as environment variables via the `.env` file (not committed to git).

```bash
# .env (not committed -- .env.example serves as template)

# PostgreSQL
POSTGRES_PASSWORD=<strong-random-password>

# Redis
REDIS_PASSWORD=<strong-random-password>

# RabbitMQ
RABBITMQ_PASSWORD=<strong-random-password>

# AWS SES
AWS_REGION=eu-west-1
AWS_ACCESS_KEY_ID=AKIA...
AWS_SECRET_ACCESS_KEY=...

# Dashboard Auth
DASHBOARD_USERNAME=admin
DASHBOARD_PASSWORD_HASH=$2b$12$...          # bcrypt hash of the dashboard password

# Application
APP_BASE_URL=https://email.israeliyonsi.dev
```

### 8.2 appsettings.json Structure (API)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithProcessId"]
  },
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=eaas;Username=eaas_app;Password=devpassword",
    "Redis": "localhost:6379"
  },
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "eaas",
    "PrefetchCount": 10
  },
  "Ses": {
    "Region": "eu-west-1",
    "AccessKeyId": "",
    "SecretAccessKey": "",
    "MaxSendRate": 14
  },
  "RateLimiting": {
    "RequestsPerSecond": 100,
    "BurstSize": 20
  },
  "Email": {
    "MaxAttachmentSizeBytes": 10485760,
    "MaxTotalAttachmentSizeBytes": 26214400,
    "MaxRecipientsPerEmail": 50,
    "MaxBatchSize": 100,
    "MaxTemplateSizeBytes": 524288,
    "LogRetentionDays": 90
  },
  "Tracking": {
    "BaseUrl": "https://email.israeliyonsi.dev",
    "PixelPath": "/track/open",
    "ClickPath": "/track/click",
    "HmacSecret": ""
  },
  "Dashboard": {
    "Username": "admin",
    "PasswordHash": ""
  }
}
```

### 8.3 Secrets Management

**Sprint 1 approach:** Environment variables via Docker Compose `.env` file. The `.env` file lives only on the server, is owned by root, and has `chmod 600` permissions. It is never committed to git.

**Why not AWS Secrets Manager or Vault:** Adds cost and complexity for a single-VPS deployment with a single operator. The attack surface is small -- if someone has SSH access to the VPS, they already have access to everything. Environment variables are the simplest secure approach for this context.

**Rotation procedure:**
1. Update the value in `.env`
2. Run `docker compose up -d <service>` to restart the affected service
3. For database password changes: update PostgreSQL first, then restart all services

### 8.4 API Key Bootstrap Mechanism

**The chicken-and-egg problem:** `POST /api/v1/keys` requires authentication via an existing API key. A mechanism is needed to create the first key.

**CLI seed command:**

```bash
# Generate the first API key (run once during initial setup)
dotnet run --project src/EaaS.Api -- seed --api-key
```

This command:
1. Generates a random API key: `eaas_live_` + 40 random alphanumeric characters
2. Computes the SHA-256 hash of the key
3. Inserts into the `api_keys` table with name "Bootstrap Key", the default tenant ID, and status `active`
4. Prints the plaintext key to stdout exactly once
5. Exits with code 0 on success, 1 if a bootstrap key already exists

**Development seed data (`scripts/seed-data.sql`):**

```sql
-- Dev-only API key for local development
-- Plaintext key: eaas_live_devkey00000000000000000000000000000000
-- SHA-256 hash of the above key
INSERT INTO api_keys (id, tenant_id, name, key_hash, prefix, status)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'Development Key',
    'b0c4de8f2a1b3c5d7e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d',
    'eaas_liv',
    'active'
);
```

**IMPORTANT:** The dev key is documented in the README and must NEVER be used in production. The `seed-data.sql` script is only mounted in `docker-compose.override.yml` (development), not in the production compose file.

**Dashboard password bootstrap:** Generate a bcrypt hash for the dashboard password using:
```bash
dotnet run --project src/EaaS.Api -- seed --dashboard-password
```
This prompts for a password, outputs the bcrypt hash, and the operator places it in the `.env` file as `DASHBOARD_PASSWORD_HASH`.

### 8.5 CORS Policy

**Decision:** No CORS headers are configured. The API is server-to-server only -- it is consumed by backend services, not browser JavaScript. If browser-based clients are needed in the future, CORS can be added as middleware with specific allowed origins.

---

## 9. NuGet Packages

### Directory.Packages.props (Central Package Management)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <!-- Framework -->
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="8.0.3" />
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.5.0" />

    <!-- EF Core + PostgreSQL -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.3" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.2" />

    <!-- MediatR -->
    <PackageVersion Include="MediatR" Version="12.2.0" />

    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="11.9.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />

    <!-- Mapping: Manual mapping via extension methods (no AutoMapper -- simpler for ~16 endpoints) -->

    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.1" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageVersion Include="Serilog.Formatting.Compact" Version="2.0.0" />
    <PackageVersion Include="Serilog.Enrichers.Environment" Version="2.3.0" />
    <PackageVersion Include="Serilog.Enrichers.Process" Version="2.0.2" />

    <!-- MassTransit + RabbitMQ -->
    <PackageVersion Include="MassTransit" Version="8.2.2" />
    <PackageVersion Include="MassTransit.RabbitMQ" Version="8.2.2" />

    <!-- Redis -->
    <PackageVersion Include="StackExchange.Redis" Version="2.7.33" />

    <!-- AWS SES -->
    <PackageVersion Include="AWSSDK.SimpleEmailV2" Version="3.7.305.4" />

    <!-- Template Engine -->
    <PackageVersion Include="Fluid.Core" Version="2.7.0" />

    <!-- DNS Lookup -->
    <PackageVersion Include="DnsClient" Version="1.7.0" />

    <!-- Health Checks -->
    <PackageVersion Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.1" />
    <PackageVersion Include="AspNetCore.HealthChecks.Redis" Version="8.0.1" />
    <PackageVersion Include="AspNetCore.HealthChecks.Rabbitmq" Version="8.0.1" />

    <!-- Security -->
    <PackageVersion Include="BCrypt.Net-Next" Version="4.0.3" />

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.7.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.7" />
    <PackageVersion Include="Moq" Version="4.20.70" />
    <PackageVersion Include="FluentAssertions" Version="6.12.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.3" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="3.8.0" />
    <PackageVersion Include="Testcontainers.Redis" Version="3.8.0" />
    <PackageVersion Include="Testcontainers.RabbitMq" Version="3.8.0" />
    <PackageVersion Include="Bogus" Version="35.4.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.1" />
  </ItemGroup>
</Project>
```

### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

### Per-Project Package References

**EaaS.Domain:** No NuGet packages (pure domain, no external dependencies).

**EaaS.Shared:** No NuGet packages.

**EaaS.Infrastructure:**
- `Microsoft.EntityFrameworkCore`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `MassTransit`
- `MassTransit.RabbitMQ`
- `StackExchange.Redis`
- `AWSSDK.SimpleEmailV2`
- `Fluid.Core`
- `DnsClient`

**EaaS.Api:**
- `Microsoft.AspNetCore.OpenApi`
- `Swashbuckle.AspNetCore`
- `MediatR`
- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Formatting.Compact`
- `Serilog.Enrichers.Environment`
- `Serilog.Enrichers.Process`
- `AspNetCore.HealthChecks.NpgSql`
- `AspNetCore.HealthChecks.Redis`
- `AspNetCore.HealthChecks.Rabbitmq`
- Project references: `EaaS.Domain`, `EaaS.Infrastructure`, `EaaS.Shared`

**EaaS.Worker:**
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Formatting.Compact`
- Project references: `EaaS.Domain`, `EaaS.Infrastructure`, `EaaS.Shared`

**EaaS.WebhookProcessor:**
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Formatting.Compact`
- Project references: `EaaS.Domain`, `EaaS.Infrastructure`, `EaaS.Shared`

**Dashboard (Next.js):**
- `next` (v15)
- `@shadcn/ui` components
- `@tanstack/react-query`
- `recharts`
- `tailwindcss`

---

## 10. Acceptance Criteria for Sprint 1

Every item must pass before Sprint 1 is considered done.

### Functional Acceptance

These criteria map directly to Sprint 1 backlog stories. Items from Sprint 2/3 (batch sending, attachments, bounce/complaint auto-suppression, dashboard) are explicitly excluded.

| # | Criterion | Backlog Story | Verification Method |
|---|-----------|---------------|-------------------|
| 1 | Can bootstrap the first API key via CLI seed command | -- (bootstrap) | `dotnet run -- seed --api-key` succeeds and prints key |
| 2 | Can create an API key via `POST /api/v1/keys` and receive the full key once | US-5.1 | cURL call returns 201 with `api_key` field |
| 3 | Can use the API key in `Authorization: Bearer` header for all subsequent calls | US-5.1 | cURL call with key returns 200; without key returns 401 |
| 4 | Can revoke an API key via `DELETE /api/v1/keys/{id}` | US-5.3 | cURL call returns 204; subsequent requests with key return 401 |
| 5 | Can add a domain via `POST /api/v1/domains` and receive DNS records | US-3.1 | cURL call returns 201 with `dns_records` array |
| 6 | Can verify a domain via `POST /api/v1/domains/{id}/verify` after DNS is configured | US-3.2 | cURL call returns 200 with `status: verified` |
| 7 | Can create a template via `POST /api/v1/templates` with Liquid syntax | US-2.1 | cURL call returns 201 with `template_id` |
| 8 | Can update a template via `PUT /api/v1/templates/{id}` (version incremented in place) | US-2.2 | cURL call returns 200 with incremented `version` |
| 9 | Can send a single email with inline HTML via `POST /api/v1/emails/send` | US-1.1 | cURL call returns 202; email arrives in inbox |
| 10 | Can send a single email using a template_id and variables | US-1.3 | cURL call returns 202; email arrives with rendered template content |
| 11 | Email is delivered to Gmail/Outlook inbox (not spam folder) | US-1.1 | Manual verification: send to test Gmail and Outlook addresses |
| 12 | Health check at `GET /health` returns all component statuses | US-0.4 | cURL returns 200 with all components healthy |
| 13 | Structured JSON logs are output by API and Worker | US-0.5 | `docker compose logs api` shows JSON-formatted log entries |
| 14 | All configuration loaded from environment variables | US-0.6 | Service starts with `.env` values; fails gracefully with missing required values |

### Infrastructure Acceptance

| # | Criterion | Backlog Story | Verification Method |
|---|-----------|---------------|-------------------|
| 15 | `docker compose up -d` starts all services without errors | US-0.1 | Run command; `docker compose ps` shows all healthy |
| 16 | Database schema created via EF Core migrations on startup | US-0.2 | Tables exist in PostgreSQL after first API start |
| 17 | Redis and RabbitMQ connected with health checks passing | US-0.3 | Health endpoint shows both components healthy |
| 18 | Services restart automatically after VPS reboot | US-0.1 | Reboot VPS; verify all services running after boot |
| 19 | RabbitMQ messages survive broker restart | US-0.3 | Stop RabbitMQ container; restart; verify queued messages still exist |
| 20 | PostgreSQL data persists across container restarts | US-0.2 | Stop PostgreSQL container; restart; verify data intact |
| 21 | TLS is active on all public endpoints | US-0.1 | `curl -v https://email.israeliyonsi.dev/health` shows valid cert |
| 22 | API responds within 200ms (p95) for single send | -- (NFR) | Load test: 100 sequential sends; measure p95 latency |

### Performance Targets

| Metric | Target | Test Method |
|--------|--------|-------------|
| API p95 response time (single send) | < 200ms | Send 100 emails sequentially, measure 95th percentile |
| Worker throughput | > 10 emails/second | Queue 1000 messages, measure total processing time |
| Health check response time | < 50ms | 100 sequential requests |

---

*End of Architecture Specification.*
