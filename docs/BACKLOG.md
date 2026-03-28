# EaaS - Groomed Product Backlog

**Grooming Date:** 2026-03-27
**Committee:** Business Analyst, Head of Design, Head of Engineering
**Source:** BRD/PRD v1.0
**Sprint Duration:** 24 hours per sprint

---

## Sprint Capacity Plan

| Sprint | Target Points | Focus | Status |
|--------|--------------|-------|--------|
| Sprint 1 (MVP) | 45 pts | Working email API: send end-to-end, auth, basic logging | Planned |
| Sprint 2 (Enhanced) | 47 pts | Tracking, analytics, dashboard, template/domain management UI | Planned |
| Sprint 3+ (Future) | 34 pts | Webhooks, multi-tenant prep, advanced features | Backlog |

---

## Sprint Overview

### Sprint 1 - MVP (45 points)

| Story ID | Title | Points | Design? | Epic |
|----------|-------|--------|---------|------|
| US-0.1 | Project scaffolding and Docker Compose setup | 5 | No | Infrastructure |
| US-0.2 | PostgreSQL schema and migration setup | 5 | No | Infrastructure |
| US-0.3 | Redis and RabbitMQ configuration | 3 | No | Infrastructure |
| US-0.4 | Health check endpoint | 2 | No | Infrastructure |
| US-0.5 | Structured logging with Serilog | 2 | No | Infrastructure |
| US-0.6 | Environment configuration management | 1 | No | Infrastructure |
| US-5.1 | Create an API key | 3 | No | API Key Management |
| US-5.3 | Revoke an API key | 2 | No | API Key Management |
| US-3.1 | Add a sending domain | 3 | No | Domain Management |
| US-3.2 | Verify domain DNS configuration | 3 | No | Domain Management |
| US-1.1 | Send a single email (end-to-end) | 8 | No | Email Sending |
| US-1.3 | Send an email using a template | 3 | No | Email Sending |
| US-2.1 | Create an email template | 3 | No | Template Management |
| US-2.2 | Update an email template | 2 | No | Template Management |
| **Total** | | **45** | | |

### Sprint 2 - Enhanced (47 points)

| Story ID | Title | Points | Design? | Epic |
|----------|-------|--------|---------|------|
| US-0.7 | API documentation (Swagger/OpenAPI) | 3 | No | Infrastructure |
| US-0.8 | CI/CD pipeline setup | 3 | No | Infrastructure |
| US-1.4 | Send an email with attachments | 3 | No | Email Sending |
| US-1.2 | Send a batch of emails | 5 | No | Email Sending |
| US-1.5 | Send an email with CC and BCC | 2 | No | Email Sending |
| US-2.3 | List and retrieve templates | 2 | No | Template Management |
| US-2.4 | Delete an email template (soft delete/restore) | 2 | No | Template Management |
| US-2.5 | Preview a template | 2 | No | Template Management |
| US-3.3 | List and view domains | 2 | No | Domain Management |
| US-6.1 | Automatic bounce suppression | 5 | No | Bounce/Complaint |
| US-6.2 | Automatic complaint suppression | 3 | No | Bounce/Complaint |
| US-4.1 | View email send logs (API) | 3 | No | Analytics/Logging |
| US-4.3 | Track email opens | 5 | No | Analytics/Logging |
| US-4.4 | Track link clicks | 5 | No | Analytics/Logging |
| US-5.4 | Rotate an API key | 2 | No | API Key Management |
| **Total** | | **47** | | |

### Sprint 3+ - Future (34 points)

| Story ID | Title | Points | Design? | Epic |
|----------|-------|--------|---------|------|
| US-7.1 | Dashboard overview page | 5 | Yes | Dashboard |
| US-7.2 | Email log viewer (dashboard) | 5 | Yes | Dashboard |
| US-7.3 | Template manager UI | 5 | Yes | Dashboard |
| US-7.4 | Domain manager UI | 3 | Yes | Dashboard |
| US-7.5 | Analytics dashboard with charts | 5 | Yes | Dashboard |
| US-7.6 | Suppression list manager UI | 3 | Yes | Dashboard |
| US-4.2 | View delivery analytics (API) | 3 | No | Analytics/Logging |
| US-6.3 | Manage suppression list (API) | 2 | No | Bounce/Complaint |
| US-3.4 | Remove a sending domain | 1 | No | Domain Management |
| US-5.2 | List and view API keys | 1 | No | API Key Management |
| US-8.1 | Configure webhook endpoints | 3 | No | Webhooks |
| US-8.2 | Receive webhook notifications | 5 | No | Webhooks |
| US-8.3 | Manage webhook endpoints | 2 | No | Webhooks |
| **Total** | | **43** | | |

---

## Epic Details

---

### Epic 0: Infrastructure & DevOps

**Description:** Foundation layer: project structure, containerization, database, caching, queuing, logging, health checks, CI/CD. Nothing works without this.

**Priority:** P0 - Sprint 1
**Design Needs:** No
**Technical Complexity:** Medium
**Dependencies:** None (this is the root dependency)

---

#### US-0.1: Project Scaffolding and Docker Compose Setup

**Story Points:** 5
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** the .NET 8 solution structure with API Server, Worker Service, and shared projects scaffolded, with Docker Compose orchestrating all services (API, Worker, PostgreSQL, Redis, RabbitMQ),
**so that** the entire stack can be started with a single `docker-compose up` command.

**Acceptance Criteria:**
1. .NET 8 solution with three projects: `EaaS.Api` (Minimal API), `EaaS.Worker` (Worker Service), `EaaS.Shared` (domain models, DTOs, interfaces).
2. Dockerfiles for API and Worker with multi-stage builds.
3. `docker-compose.yml` with services: api, worker, postgres, redis, rabbitmq.
4. All services start and communicate on a shared Docker network.
5. Volumes configured for PostgreSQL and RabbitMQ data persistence.
6. `.env.example` file with all required environment variables documented.

**Grooming Notes:**
- **Engineering:** Start from `dotnet new` templates. Keep Dockerfiles lean (alpine base). Use `docker-compose.override.yml` for local dev overrides.
- **BA:** This is the critical path blocker. Nothing else can start until this is done.

---

#### US-0.2: PostgreSQL Schema and Migration Setup

**Story Points:** 5
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** the initial database schema created via EF Core migrations covering all core entities,
**so that** the API and Worker have persistent storage from day one.

**Acceptance Criteria:**
1. EF Core DbContext with entity configurations for: `EmailMessage`, `Template`, `TemplateVersion`, `Domain`, `DnsRecord`, `ApiKey`, `SuppressionEntry`, `EmailLog`.
2. Initial migration creates all tables with appropriate indexes.
3. Migration runs automatically on application startup (development) or via CLI (production).
4. Connection string loaded from environment variables.
5. UUID primary keys for all entities.
6. `created_at`, `updated_at` audit columns on all entities.

**Grooming Notes:**
- **Engineering:** Use Npgsql + EF Core. Design for 90-day log retention from the start (add `sent_at` index for partition-ready cleanup). Do NOT set up table partitioning yet -- simple date-based cleanup job is sufficient for Phase 1 volumes.
- **BA:** Schema must support all P0 user stories. Include soft-delete columns (`deleted_at`) on Templates.

---

#### US-0.3: Redis and RabbitMQ Configuration

**Story Points:** 3
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** Redis and RabbitMQ configured with connection abstractions and health checks,
**so that** the API can use Redis for rate limiting/caching and RabbitMQ for message queuing.

**Acceptance Criteria:**
1. Redis connection via StackExchange.Redis with connection multiplexer registered in DI.
2. RabbitMQ connection via MassTransit (or RabbitMQ.Client) with exchanges, queues, and dead-letter queue configured.
3. Queue topology: `email-send` queue with `email-send-dlq` dead-letter queue, 3 retry attempts with exponential backoff.
4. Connection strings from environment variables.
5. Graceful reconnection on transient failures.

**Grooming Notes:**
- **Engineering:** MassTransit is the preferred abstraction -- it handles retry, DLQ, and consumer patterns out of the box for .NET. Dead-letter configuration is the most complex part. Test the retry/DLQ flow explicitly before moving on.
- **BA:** The DLQ pattern is critical for the reliability NFR (zero message loss).

---

#### US-0.4: Health Check Endpoint

**Story Points:** 2
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** a `GET /health` endpoint that reports the status of all dependencies,
**so that** I can monitor system health and detect failures.

**Acceptance Criteria:**
1. `GET /health` returns JSON with status of: API, PostgreSQL, Redis, RabbitMQ, AWS SES connectivity.
2. Returns 200 if all healthy, 503 if any critical component is down.
3. Each component reports `status` and `latency_ms`.
4. Response matches the contract in the BRD/PRD section 14.
5. Endpoint is unauthenticated (accessible without API key).

**Grooming Notes:**
- **Engineering:** Use ASP.NET Core health checks (`Microsoft.Extensions.Diagnostics.HealthChecks`). SES check can be a simple `GetSendQuota` API call. Keep timeout low (5s per component).

---

#### US-0.5: Structured Logging with Serilog

**Story Points:** 2
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** structured JSON logging via Serilog across both API and Worker,
**so that** I can debug issues from Docker log output.

**Acceptance Criteria:**
1. Serilog configured with Console sink (JSON format) for Docker log capture.
2. Correlation ID (`X-Request-Id` header or generated) propagated across API request and Worker processing.
3. Log levels: Debug, Information, Warning, Error, Fatal.
4. Sensitive data (API keys, email bodies) excluded from logs.
5. Request/response logging middleware on API (status code, duration, path -- no bodies).

**Grooming Notes:**
- **Engineering:** Use `Serilog.AspNetCore` enricher. The correlation ID must flow from API through RabbitMQ message headers to Worker logs.

---

#### US-0.6: Environment Configuration Management

**Story Points:** 1
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** all configuration loaded from environment variables with a strongly-typed options pattern,
**so that** settings are centralized and validated at startup.

**Acceptance Criteria:**
1. `IOptions<T>` pattern for: `DatabaseOptions`, `RedisOptions`, `RabbitMqOptions`, `SesOptions`, `ApiOptions`.
2. Startup validation fails fast if required settings are missing.
3. `.env.example` documents every variable with descriptions and defaults.
4. No secrets in source code or configuration files.

**Grooming Notes:**
- **Engineering:** Straightforward. Use `OptionsBuilder.ValidateDataAnnotations().ValidateOnStart()`.

---

#### US-0.7: API Documentation (Swagger/OpenAPI)

**Story Points:** 3
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** auto-generated OpenAPI/Swagger documentation for all API endpoints,
**so that** I can explore and test the API interactively.

**Acceptance Criteria:**
1. Swagger UI available at `/swagger` in development.
2. OpenAPI 3.0 spec generated from endpoint metadata.
3. All request/response models documented with examples.
4. Authentication scheme (Bearer token) documented.
5. Spec exportable as JSON for external tooling.

**Grooming Notes:**
- **Engineering:** Use `Swashbuckle.AspNetCore` or `NSwag`. Add XML comments to endpoint methods.

---

#### US-0.8: CI/CD Pipeline Setup

**Story Points:** 3
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** a GitHub Actions pipeline that builds, tests, and packages Docker images,
**so that** every push is validated and deployment is automated.

**Acceptance Criteria:**
1. GitHub Actions workflow triggers on push to `main` and `dev`, and on PRs.
2. Steps: restore, build, test, Docker image build.
3. Docker images tagged with commit SHA and `latest`.
4. Images pushed to GitHub Container Registry (ghcr.io).
5. Deployment step (SSH to Hetzner, pull images, docker-compose up) on merge to `main`.

**Grooming Notes:**
- **Engineering:** Keep the pipeline simple. No Kubernetes, no Helm. Just SSH + docker-compose pull + up.

---

### Epic 1: Email Sending

**Description:** The core capability: accept email send requests via API, enqueue to RabbitMQ, process via Worker, deliver through AWS SES.

**Priority:** P0 - Sprint 1 (single send + template send), Sprint 2 (batch, attachments, CC/BCC)
**Design Needs:** No (API-only)
**Technical Complexity:** High
**Dependencies:** Epic 0 (Infrastructure)

---

#### US-1.1: Send a Single Email (End-to-End)

**Story Points:** 8
**Sprint:** 1
**Design Needed:** No

**As a** developer integrating with EaaS,
**I want** to send a single transactional email via the API,
**so that** my application can notify users without managing SMTP directly.

**Acceptance Criteria:**
1. `POST /api/v1/emails/send` accepts `to`, `from`, `subject`, `html_body` (and optional `text_body`).
2. API validates all required fields and returns 400 with descriptive errors for invalid input.
3. API authenticates the request via `Authorization: Bearer` header (API key lookup).
4. API returns 202 Accepted with a unique `message_id` within 200ms (p95).
5. The email is enqueued to RabbitMQ for async processing.
6. Worker picks up the message, delivers via AWS SES SMTP/API, and logs the result in PostgreSQL.
7. If the recipient is on the suppression list, the API returns 422 with a clear error message.
8. The `from` address must belong to a verified domain.
9. `GET /api/v1/emails/{message_id}` returns current status of the email.

**Grooming Notes:**
- **Engineering:** This is the single most critical story. It spans API + Worker + SES + PostgreSQL + RabbitMQ. Break into sub-tasks: (1) API endpoint + validation + auth middleware, (2) RabbitMQ producer, (3) Worker consumer + SES delivery, (4) status logging. The 8 points reflect end-to-end complexity.
- **Engineering Risk:** AWS SES sandbox mode limits sending to verified addresses only. The developer must request production access early. Plan for a 1-3 day wait.
- **BA:** The response contract must match the API spec in section 14 exactly. Tags and metadata fields should be accepted and stored even if not yet queryable.

---

#### US-1.3: Send an Email Using a Template

**Story Points:** 3
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** to send an email by referencing a stored template and passing variables,
**so that** I don't have to embed HTML in my API calls.

**Acceptance Criteria:**
1. `POST /api/v1/emails/send` accepts `template_id` and `variables` instead of `html_body`.
2. Worker renders the template with provided variables using Fluid (Liquid) before sending.
3. If `template_id` does not exist, API returns 404.
4. If required template variables are missing, API returns 400 listing the missing variables.
5. Template rendering errors are logged and the email is moved to the dead-letter queue.

**Grooming Notes:**
- **Engineering:** Depends on US-2.1 (templates must exist). Use the Fluid library for Liquid syntax. Cache compiled templates in Redis. The variable schema validation is a nice touch from the BRD -- implement it as JSON Schema validation against `variables_schema`.
- **BA:** Edge case: what if both `html_body` and `template_id` are provided? Decision: `template_id` takes precedence. Document in API docs.

---

#### US-1.4: Send an Email with Attachments

**Story Points:** 3
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to attach files to transactional emails,
**so that** I can send invoices, receipts, and documents to users.

**Acceptance Criteria:**
1. `attachments` array with `filename`, `content` (base64), `content_type`.
2. Individual attachment limit: 10MB. Total per email: 25MB.
3. API returns 400 if size limits exceeded.
4. Attachments passed through to SES.
5. Supported types: PDF, PNG, JPEG, CSV, XLSX.

**Grooming Notes:**
- **Engineering:** Base64 decoding increases request payload size by ~33%. Validate before enqueuing. RabbitMQ message size may need config adjustment for large attachments. Consider storing attachments in a temp location and passing a reference rather than the full content through the queue.
- **BA:** The 25MB total aligns with SES limits. No concerns.

---

#### US-1.2: Send a Batch of Emails

**Story Points:** 5
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to send up to 100 emails in a single API call,
**so that** I can efficiently send bulk transactional emails.

**Acceptance Criteria:**
1. `POST /api/v1/emails/batch` accepts array of email objects (max 100).
2. Each email validated independently; partial failures reported per-item.
3. Returns 202 with array of `message_id` values and a `batch_id`.
4. All emails enqueued individually to RabbitMQ.
5. Suppressed recipients skipped with per-item error.

**Grooming Notes:**
- **Engineering:** Each email in the batch becomes an independent RabbitMQ message. The `batch_id` is a correlation key for querying. Validate all emails before enqueuing any (fail-fast for obvious errors, but allow partial success for suppressions).
- **BA:** Acceptance criteria are clear. Add: batch status query endpoint (`GET /api/v1/emails/batch/{batch_id}`) as a follow-up if needed.

---

#### US-1.5: Send an Email with CC and BCC

**Story Points:** 2
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to include CC and BCC recipients on transactional emails,
**so that** I can copy relevant parties.

**Acceptance Criteria:**
1. Optional `cc` and `bcc` arrays accepted.
2. CC visible in headers; BCC not.
3. Addresses validated for format.
4. Suppressed addresses in CC/BCC silently removed (logged as skipped).
5. Combined `to` + `cc` + `bcc` must not exceed 50.

**Grooming Notes:**
- **Engineering:** Straightforward extension to the send endpoint. SES supports CC/BCC natively. Just add fields to the message model and SES request builder.

---

### Epic 2: Template Management

**Description:** CRUD operations for email templates with Liquid syntax, variable schemas, versioning, and caching.

**Priority:** P0 (create/update in Sprint 1), P1 (list/delete/preview in Sprint 2)
**Design Needs:** No (API-only; dashboard UI is Epic 7)
**Technical Complexity:** Medium
**Dependencies:** Epic 0 (database, Redis)

---

#### US-2.1: Create an Email Template

**Story Points:** 3
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** to create and store email templates with variable placeholders,
**so that** I can reuse them across multiple sends.

**Acceptance Criteria:**
1. `POST /api/v1/templates` accepts `name`, `subject_template`, `html_body`, `text_body` (optional), `variables_schema`.
2. Template names unique per account.
3. Liquid syntax validated on creation.
4. Stored in PostgreSQL, cached in Redis.
5. Returns 201 with `template_id`.
6. Max template size: 512KB.

**Grooming Notes:**
- **Engineering:** Use Fluid library to parse and validate the template syntax on save. Store the raw template, not the compiled form. Redis cache key: `template:{id}:v{version}`.
- **BA:** The `variables_schema` (JSON Schema) is a differentiator vs. competitors. It enables the API to validate variables at send time rather than failing silently during rendering.

---

#### US-2.2: Update an Email Template

**Story Points:** 2
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** to update an existing template,
**so that** I can iterate on email content.

**Acceptance Criteria:**
1. `PUT /api/v1/templates/{id}` accepts updatable fields.
2. Previous version retained (last 10 versions).
3. Redis cache invalidated on update.
4. Returns 200 with updated template.
5. Active sends using old version complete with old content.

**Grooming Notes:**
- **Engineering:** Implement versioning via a `TemplateVersion` table. On update, insert new version, update the `current_version` pointer. The Worker fetches the template version that was current at enqueue time (include version in the RabbitMQ message).

---

#### US-2.3: List and Retrieve Templates

**Story Points:** 2
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to list all templates and retrieve a specific template by ID,
**so that** I can manage my template library.

**Acceptance Criteria:**
1. `GET /api/v1/templates` returns paginated list (default 20, max 100).
2. `GET /api/v1/templates/{id}` returns full template with body and schema.
3. Supports filtering by `name` and sorting by `created_at`/`updated_at`.
4. Response includes `version`, `created_at`, `updated_at`, `send_count`.

**Grooming Notes:**
- **Engineering:** Standard pagination pattern. Use cursor-based or offset pagination. `send_count` can be a computed field from the email logs table.

---

#### US-2.4: Delete an Email Template (Soft Delete/Restore)

**Story Points:** 2
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to delete a template that is no longer needed,
**so that** I can keep my library clean.

**Acceptance Criteria:**
1. `DELETE /api/v1/templates/{id}` soft-deletes (sets `deleted_at`).
2. Excluded from list results.
3. Sends referencing deleted template return 404.
4. Redis cache entry removed.
5. Restorable within 30 days via `PATCH /api/v1/templates/{id}/restore`.

**Grooming Notes:**
- **Engineering:** Soft delete pattern is already in the schema (US-0.2). Add a global query filter in EF Core to exclude soft-deleted records.

---

#### US-2.5: Preview a Template

**Story Points:** 2
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to preview a rendered template with sample variables,
**so that** I can verify output before sending.

**Acceptance Criteria:**
1. `POST /api/v1/templates/{id}/preview` accepts `variables` object.
2. Returns rendered HTML and text bodies without sending.
3. Missing variables highlighted in output.
4. Includes rendered subject line.

**Grooming Notes:**
- **Engineering:** Reuse the same Fluid rendering pipeline from the Worker. No RabbitMQ involvement -- synchronous render and return.

---

### Epic 3: Domain Management

**Description:** Register sending domains, generate DNS records (SPF/DKIM/DMARC), and verify configuration against AWS SES.

**Priority:** P0 (add/verify in Sprint 1), P1 (list in Sprint 2), P2 (remove in Sprint 3+)
**Design Needs:** No (API-only; dashboard UI is Epic 7)
**Technical Complexity:** High
**Dependencies:** Epic 0 (database), AWS SES account

---

#### US-3.1: Add a Sending Domain

**Story Points:** 3
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** to register a domain for sending emails,
**so that** I can send from addresses on that domain.

**Acceptance Criteria:**
1. `POST /api/v1/domains` accepts domain name.
2. System calls AWS SES to create domain identity and generates DNS records (SPF, DKIM x3, DMARC).
3. Returns the DNS records the user must configure.
4. Domain status set to `pending_verification`.
5. Duplicate domains rejected with 409.

**Grooming Notes:**
- **Engineering Risk:** This requires the AWS SES SDK (`VerifyDomainIdentity`, `VerifyDomainDkim`). The DKIM CNAME records are generated by SES. SPF and DMARC records are standard patterns. The complexity is in correctly mapping SES responses to the DNS record format.
- **BA:** The response must include all 5 DNS records (1 SPF + 3 DKIM + 1 DMARC) in the exact format shown in the API contract.

---

#### US-3.2: Verify Domain DNS Configuration

**Story Points:** 3
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** to trigger domain verification after configuring DNS,
**so that** I can start sending from that domain.

**Acceptance Criteria:**
1. `POST /api/v1/domains/{id}/verify` triggers DNS record verification.
2. Checks SPF, DKIM, DMARC against expected values.
3. Each record type independently reported as verified/failed.
4. All pass: domain status becomes `verified`.
5. Daily re-verification for all verified domains.
6. Becomes `suspended` if re-check fails.

**Grooming Notes:**
- **Engineering:** Use `DnsClient` NuGet package for DNS resolution. SES also provides its own verification status via `GetIdentityVerificationAttributes`. Combine both: check SES status AND do direct DNS lookups for the full picture. The daily re-verification can be a simple background timer in the Worker -- Sprint 2 is fine for that part.
- **BA:** Clarification: daily re-verification (AC #5) is P1. Sprint 1 only needs manual verify trigger.

---

#### US-3.3: List and View Domains

**Story Points:** 2
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to list all domains and their verification status,
**so that** I can manage my sending domains.

**Acceptance Criteria:**
1. `GET /api/v1/domains` returns all domains with status, DNS records, verification timestamps.
2. `GET /api/v1/domains/{id}` returns full detail with individual DNS record statuses.
3. Includes `last_verified_at` and next scheduled verification time.

**Grooming Notes:**
- **Engineering:** Straightforward read endpoints. No concerns.

---

#### US-3.4: Remove a Sending Domain

**Story Points:** 1
**Sprint:** 3+
**Design Needed:** No

**As a** developer,
**I want** to remove a domain I no longer use,
**so that** I can keep my domain list clean.

**Acceptance Criteria:**
1. `DELETE /api/v1/domains/{id}` removes the domain.
2. Queued emails for that domain rejected.
3. Future sends from that domain return 400.
4. SES domain identity removed.

**Grooming Notes:**
- **Engineering:** Must call SES `DeleteIdentity`. Need to handle the queue edge case carefully.

---

### Epic 4: Analytics and Logging

**Description:** Email send logs, delivery analytics, open tracking (pixel), and click tracking (link rewriting).

**Priority:** P1 (Sprint 2 for logs and tracking), P2 (Sprint 3+ for analytics API)
**Design Needs:** No (API-only; dashboard charts are Epic 7)
**Technical Complexity:** High (tracking pixel + link rewriting are complex)
**Dependencies:** Epic 1 (email sending must work first)

---

#### US-4.1: View Email Send Logs (API)

**Story Points:** 3
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to view a log of all sent emails with delivery status,
**so that** I can troubleshoot delivery issues.

**Acceptance Criteria:**
1. `GET /api/v1/logs` returns paginated logs with: `message_id`, `to`, `from`, `subject`, `status`, timestamps.
2. Filtering by: status, to, from, date_range, api_key_id, template_id.
3. Sorting by `sent_at` (default desc).
4. 50 items default, 200 max per page.
5. `GET /api/v1/logs/{message_id}` returns full detail with all status transitions.
6. 90-day retention.

**Grooming Notes:**
- **Engineering:** The email log data is already being written in US-1.1. This story adds the query endpoints with filtering. Index `sent_at`, `status`, `to_email` columns for query performance.
- **BA:** The 90-day retention is a background cleanup job. Can be a simple SQL `DELETE WHERE sent_at < NOW() - INTERVAL '90 days'` running daily.

---

#### US-4.2: View Delivery Analytics (API)

**Story Points:** 3
**Sprint:** 3+
**Design Needed:** No

**As a** developer,
**I want** aggregate delivery statistics,
**so that** I can monitor email health.

**Acceptance Criteria:**
1. `GET /api/v1/analytics` returns aggregate stats for date range.
2. Metrics: total_sent, delivered, bounced, complained, opened, clicked, rates.
3. Grouping by day/week/month.
4. Filtering by domain, api_key_id, template_id.
5. Default range: last 30 days.
6. Optimized for charting (time-series array).

**Grooming Notes:**
- **Engineering:** Consider a materialized view or a daily aggregate table to avoid scanning the full logs table on every request. For Phase 1 volumes (<5K/month), direct queries are fine.

---

#### US-4.3: Track Email Opens

**Story Points:** 5
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to know when recipients open my emails,
**so that** I can understand engagement.

**Acceptance Criteria:**
1. Open tracking enabled by default (configurable via `track_opens: false`).
2. 1x1 transparent tracking pixel injected into HTML body by Worker before sending.
3. Pixel load records `opened` event with timestamp and country (IP geolocation).
4. Multiple opens recorded; `first_opened_at` tracked separately.
5. Only applies to HTML emails.

**Grooming Notes:**
- **Engineering Risk:** This requires a public-facing tracking endpoint (`GET /t/{tracking_id}.gif`) that must be fast (<50ms). The endpoint must: (1) record the open event, (2) return the 1x1 GIF. Use an in-memory GIF constant. IP geolocation can use a free MaxMind GeoLite2 database or a simple lookup service. The tracking pixel URL must be unique per message (include `message_id` hash).
- **Engineering:** The Worker must rewrite the HTML body to inject the pixel before sending via SES. This is an HTML manipulation task -- use AngleSharp or simple string append before `</body>`.
- **BA:** Privacy consideration: document that open tracking relies on image loading, which many email clients block. Open rates will be underreported. This is industry-standard behavior.

---

#### US-4.4: Track Link Clicks

**Story Points:** 5
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to know when recipients click links in my emails,
**so that** I can understand which content drives action.

**Acceptance Criteria:**
1. Click tracking enabled by default (configurable via `track_clicks: false`).
2. Links in HTML body rewritten to pass through EaaS tracking endpoint.
3. Click event recorded with original URL, timestamp, user agent.
4. User redirected to original URL (<100ms).
5. Multiple clicks recorded individually.
6. Unsubscribe links (containing `unsubscribe`) never rewritten.

**Grooming Notes:**
- **Engineering Risk:** Link rewriting is the most complex tracking feature. The Worker must: (1) parse all `<a href>` tags in the HTML, (2) replace each URL with a tracking URL (`https://email.israeliyonsi.dev/c/{click_id}`), (3) store the original URL mapping. The redirect endpoint must be extremely fast -- Redis lookup for the original URL, 302 redirect. Use AngleSharp for reliable HTML parsing.
- **Engineering:** The `click_id` must encode both the message and the specific link. Consider `{message_id}:{link_index}` hashed to a short ID. Store mappings in Redis with a 90-day TTL matching log retention.
- **BA:** Edge case: what about links in plain-text emails? Decision: click tracking only applies to HTML body. Plain text links are not rewritten.

---

### Epic 5: API Key Management

**Description:** Create, revoke, rotate, and list API keys for authenticating calling applications.

**Priority:** P0 (create/revoke in Sprint 1), P1 (rotate in Sprint 2), P2 (list in Sprint 3+)
**Design Needs:** No
**Technical Complexity:** Low
**Dependencies:** Epic 0 (database)

---

#### US-5.1: Create an API Key

**Story Points:** 3
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** to create API keys scoped to specific applications,
**so that** I can track sending per-app and revoke access independently.

**Acceptance Criteria:**
1. `POST /api/v1/api-keys` accepts `name` and optional `allowed_domains`.
2. Full key returned only once. Stored as SHA-256 hash.
3. Format: `eaas_live_` + 40 random alphanumeric characters.
4. Immediately active.
5. Response includes `key_id`, `name`, `created_at`, `prefix` (first 8 chars).

**Grooming Notes:**
- **Engineering:** Use `RandomNumberGenerator` for cryptographically secure key generation. Store SHA-256 hash + prefix for identification. The auth middleware hashes the incoming key and looks up the hash. Index the hash column.
- **BA:** Critical question: how is the FIRST API key created? Sprint 1 needs a seed/bootstrap mechanism -- either a CLI command, a migration seed, or a dashboard-based creation with password auth. Decision: use a CLI command (`dotnet run -- seed-apikey`) or environment variable for the initial key.

---

#### US-5.3: Revoke an API Key

**Story Points:** 2
**Sprint:** 1
**Design Needed:** No

**As a** developer,
**I want** to revoke a compromised or unused API key,
**so that** I can prevent unauthorized access.

**Acceptance Criteria:**
1. `DELETE /api/v1/api-keys/{id}` immediately revokes.
2. Returns 401 on subsequent use.
3. Already-queued emails continue processing.
4. Revocation logged with timestamp.

**Grooming Notes:**
- **Engineering:** Set `revoked_at` timestamp. Auth middleware checks `revoked_at IS NULL`. Cache active key hashes in Redis for fast lookup; invalidate cache on revoke.

---

#### US-5.4: Rotate an API Key

**Story Points:** 2
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** to rotate an API key without downtime,
**so that** I can maintain security hygiene.

**Acceptance Criteria:**
1. `POST /api/v1/api-keys/{id}/rotate` generates new key, returns it.
2. Old key valid for configurable grace period (default 24 hours).
3. After grace period, old key auto-revoked.
4. Both keys work during grace period.
5. New key inherits all permissions.

**Grooming Notes:**
- **Engineering:** Create a new key record linked to the old one. Background job (or Worker timer) revokes expired grace-period keys. Simple implementation: store `grace_expires_at` on the old key.

---

#### US-5.2: List and View API Keys

**Story Points:** 1
**Sprint:** 3+
**Design Needed:** No

**As a** developer,
**I want** to list all API keys with metadata,
**so that** I can manage application access.

**Acceptance Criteria:**
1. `GET /api/v1/api-keys` returns all keys with: `key_id`, `name`, `prefix`, `created_at`, `last_used_at`, `status`, `send_count`.
2. Full key never returned after creation.
3. Includes usage statistics.

**Grooming Notes:**
- **Engineering:** `last_used_at` requires updating a timestamp on every authenticated request. Use Redis to buffer these updates and flush to PostgreSQL periodically (every 60s) to avoid write amplification.

---

### Epic 6: Bounce and Complaint Handling

**Description:** Process AWS SES bounce/complaint notifications via SNS webhooks. Auto-suppress problematic addresses. Manage suppression list.

**Priority:** P1 (Sprint 2 for auto-suppression), P2 (Sprint 3+ for management API)
**Design Needs:** No
**Technical Complexity:** High
**Dependencies:** Epic 1 (email sending), AWS SES + SNS configuration

---

#### US-6.1: Automatic Bounce Suppression

**Story Points:** 5
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** hard-bounced addresses automatically suppressed,
**so that** I don't hurt my sender reputation.

**Acceptance Criteria:**
1. Hard bounce from SES (via SNS webhook) adds recipient to suppression list.
2. Soft bounces retry 3x with exponential backoff (1m, 5m, 30m).
3. 3 consecutive soft bounces for same address triggers suppression.
4. Suppressed addresses cached in Redis for O(1) lookup.
5. Emails to suppressed addresses rejected at API level (never enqueued).

**Grooming Notes:**
- **Engineering Risk:** This requires setting up an SNS topic subscription in AWS, configuring SES to publish bounce notifications to that topic, and creating an endpoint in the API (`POST /api/v1/webhooks/ses/bounce`) to receive SNS notifications. SNS sends a confirmation request first that must be handled. The SNS message format for bounces includes recipient details and bounce type.
- **Engineering:** The soft-bounce retry counter needs to be tracked per-address. Use Redis with a TTL-based counter (`bounce:{email}:soft_count` with 24h TTL). The suppression check in the API should be a Redis `SISMEMBER` on a set.
- **BA:** This is a sender reputation protection mechanism. Without it, the SES account can be suspended. Non-negotiable for production use.

---

#### US-6.2: Automatic Complaint Suppression

**Story Points:** 3
**Sprint:** 2
**Design Needed:** No

**As a** developer,
**I want** addresses that file spam complaints automatically suppressed,
**so that** I maintain complaint rate below SES thresholds.

**Acceptance Criteria:**
1. Complaint from SES (via SNS) immediately suppresses the address.
2. Complaint events logged with feedback type.
3. Warning logged at 0.08% complaint rate.
4. Sending paused at 0.1% with dashboard alert.

**Grooming Notes:**
- **Engineering:** Shares the SNS webhook infrastructure with US-6.1. Complaint rate calculation: complaints in last 24h / emails sent in last 24h. Store counts in Redis. The "pause sending" mechanism is a global circuit breaker flag checked by the Worker before each send.
- **BA:** The 0.1% threshold is an AWS SES hard limit. Exceeding it results in account review and possible suspension.

---

#### US-6.3: Manage Suppression List (API)

**Story Points:** 2
**Sprint:** 3+
**Design Needed:** No

**As a** developer,
**I want** to view and manage the suppression list,
**so that** I can manually add or remove addresses.

**Acceptance Criteria:**
1. `GET /api/v1/suppressions` returns paginated list with email, reason, timestamp, source_message_id.
2. `POST /api/v1/suppressions` manually adds an address.
3. `DELETE /api/v1/suppressions/{email}` removes (with confirmation warning for bounces/complaints).
4. Changes are audit-logged.

**Grooming Notes:**
- **Engineering:** Standard CRUD. Redis suppression set must be updated on manual add/remove.

---

### Epic 7: Dashboard (Blazor Server)

**Description:** Web UI for monitoring email delivery, managing templates/domains, viewing analytics, and managing suppressions.

**Priority:** P2 (Sprint 3+) -- all dashboard stories deferred
**Design Needs:** Yes -- all stories require UI/UX wireframes and mockups
**Technical Complexity:** Medium
**Dependencies:** All API epics (dashboard consumes API endpoints)

**Grooming Committee Decision:** The entire Dashboard epic is deferred to Sprint 3+. Rationale: with a 24-hour sprint cycle, the MVP must focus on the API pipeline. The API endpoints provide full functionality -- the dashboard is a convenience layer. Israel can use Swagger UI, `curl`, or a simple Postman collection to manage the system until the dashboard is built.

---

#### US-7.1: Dashboard Overview Page

**Story Points:** 5
**Sprint:** 3+
**Design Needed:** Yes -- needs wireframe for layout, metric cards, chart placement, alert panel

**Acceptance Criteria:**
1. Shows: total sent (today, 7d, 30d), delivery rate, bounce rate, complaint rate, open rate, click rate.
2. Line chart: daily send volume for 30 days.
3. System health indicator (API, Worker, queue depth).
4. Recent alerts panel.
5. Loads in under 2 seconds.

**Grooming Notes:**
- **Design:** Needs wireframe before implementation. Metric card layout, chart library selection (Chart.js via Blazor interop or a Blazor-native chart library like MudBlazor Charts).
- **Engineering:** Blazor Server with SignalR for real-time updates. Use MudBlazor component library for rapid UI development.

---

#### US-7.2: Email Log Viewer (Dashboard)

**Story Points:** 5
**Sprint:** 3+
**Design Needed:** Yes -- needs wireframe for table layout, filter panel, detail slide-out

**Acceptance Criteria:**
1. Searchable, filterable table of email logs.
2. Filters: status, date range, recipient, sender, template, API key.
3. Click row for full detail view.
4. Text search across subject and recipient.
5. Column sorting and pagination (50 rows/page).

**Grooming Notes:**
- **Design:** Data table with filter sidebar. Detail view as slide-out panel or modal.

---

#### US-7.3: Template Manager UI

**Story Points:** 5
**Sprint:** 3+
**Design Needed:** Yes -- needs wireframe for template list, editor with preview pane, version history

**Acceptance Criteria:**
1. List templates with name, version, send count, last updated.
2. Code editor with Liquid/HTML syntax highlighting.
3. Live preview with sample variables.
4. Variable schema editor.
5. Version history with diff view.

**Grooming Notes:**
- **Design:** Two-panel layout: editor on left, preview on right. Version history as a dropdown or sidebar.
- **Engineering:** Syntax highlighting in Blazor requires a JS interop library (Monaco Editor or CodeMirror). This is the most complex dashboard page.

---

#### US-7.4: Domain Manager UI

**Story Points:** 3
**Sprint:** 3+
**Design Needed:** Yes -- needs wireframe for domain list, DNS record display, verification wizard

**Acceptance Criteria:**
1. List domains with color-coded status (green/yellow/red).
2. Adding domain shows copyable DNS records.
3. "Verify Now" button.
4. Individual DNS record status displayed.

**Grooming Notes:**
- **Design:** Wizard-style flow for adding a domain: enter domain -> show DNS records -> verify. Status dashboard with color indicators.

---

#### US-7.5: Analytics Dashboard with Charts

**Story Points:** 5
**Sprint:** 3+
**Design Needed:** Yes -- needs wireframe for chart layout, filter controls, drill-down interaction

**Acceptance Criteria:**
1. Time-series chart: sends, deliveries, bounces, complaints.
2. Pie/donut: delivery status breakdown.
3. Bar charts: top templates, top domains.
4. Per-API-key stats table.
5. Date range selector with presets.
6. Interactive charts (hover, click to drill down).

**Grooming Notes:**
- **Design:** Dashboard layout with 2x2 chart grid, date picker at top, filter sidebar.
- **Engineering:** Chart.js via JS interop or ApexCharts. Data comes from the analytics API (US-4.2).

---

#### US-7.6: Suppression List Manager UI

**Story Points:** 3
**Sprint:** 3+
**Design Needed:** Yes -- needs wireframe for suppression table, search, bulk actions

**Acceptance Criteria:**
1. Table of suppressed addresses with reason, date, source.
2. Search by email (partial match).
3. Bulk remove selected.
4. Manual add with reason.
5. CSV export.

**Grooming Notes:**
- **Design:** Standard data table with search bar, bulk action toolbar, and add modal.

---

### Epic 8: Webhook Notifications

**Description:** Allow calling applications to register webhook URLs to receive real-time delivery event notifications.

**Priority:** P2 (Sprint 3+)
**Design Needs:** No
**Technical Complexity:** Medium
**Dependencies:** Epic 1 (email sending), Epic 4 (event tracking)

---

#### US-8.1: Configure Webhook Endpoints

**Story Points:** 3
**Sprint:** 3+
**Design Needed:** No

**Acceptance Criteria:**
1. `POST /api/v1/webhooks` accepts URL, event types, optional secret.
2. Events: sent, delivered, bounced, complained, opened, clicked, failed.
3. HTTPS required.
4. Test ping to verify reachability.
5. Max 10 endpoints.

---

#### US-8.2: Receive Webhook Notifications

**Story Points:** 5
**Sprint:** 3+
**Design Needed:** No

**Acceptance Criteria:**
1. POST with JSON body: event, message_id, timestamp, data.
2. HMAC-SHA256 signature header.
3. Retry 5x with exponential backoff on failure.
4. Failed deliveries logged and surfaced on dashboard.
5. Delivery latency < 5 seconds.

**Grooming Notes:**
- **Engineering:** This is effectively a "webhook producer" -- a separate background process in the Worker that listens for email events and dispatches HTTP calls. Needs its own retry queue (or reuse RabbitMQ with a separate `webhook-delivery` queue).

---

#### US-8.3: Manage Webhook Endpoints

**Story Points:** 2
**Sprint:** 3+
**Design Needed:** No

**Acceptance Criteria:**
1. `GET /api/v1/webhooks` lists all endpoints with stats.
2. `PUT /api/v1/webhooks/{id}` updates URL/events/secret.
3. `DELETE /api/v1/webhooks/{id}` removes endpoint.
4. Delivery logs visible (last 100 per endpoint).

---

## Technical Risk Register

| # | Risk | Severity | Sprint | Mitigation |
|---|------|----------|--------|------------|
| 1 | AWS SES sandbox mode blocks sending to unverified recipients | High | Sprint 1 | Request production access immediately on day 1. Test with verified addresses until approved. |
| 2 | RabbitMQ dead-letter queue misconfiguration causes message loss | High | Sprint 1 | Write integration test that verifies DLQ routing. Test with intentionally failing messages. |
| 3 | Link rewriting breaks email HTML | Medium | Sprint 2 | Use AngleSharp for robust HTML parsing. Test with diverse real-world email templates. |
| 4 | Tracking pixel blocked by email clients | Low | Sprint 2 | Expected behavior. Document that open rates are approximate. No mitigation needed. |
| 5 | Large attachments cause RabbitMQ performance issues | Medium | Sprint 2 | Consider storing attachments on disk/S3 and passing references through the queue instead of raw content. |
| 6 | SNS webhook processing fails silently | High | Sprint 2 | Add health check for SNS subscription status. Log every SNS notification received. Alert on processing failures. |

---

## Dependency Graph

```
Sprint 1 Critical Path:
  US-0.1 (Scaffolding)
    -> US-0.2 (Database) + US-0.3 (Redis/RabbitMQ) + US-0.5 (Logging) + US-0.6 (Config)
      -> US-0.4 (Health Check)
      -> US-5.1 (Create API Key) -> US-5.3 (Revoke Key)
      -> US-3.1 (Add Domain) -> US-3.2 (Verify Domain)
      -> US-2.1 (Create Template) -> US-2.2 (Update Template)
      -> US-1.1 (Send Email - depends on all above) -> US-1.3 (Template Send)

Sprint 2 builds on Sprint 1:
  US-1.1 -> US-1.4 (Attachments), US-1.2 (Batch), US-1.5 (CC/BCC)
  US-1.1 -> US-4.1 (Logs API), US-4.3 (Open Tracking), US-4.4 (Click Tracking)
  US-1.1 -> US-6.1 (Bounce Handling), US-6.2 (Complaint Handling)

Sprint 3+ builds on Sprint 2:
  US-4.3 + US-4.4 -> US-4.2 (Analytics API)
  US-4.2 -> US-7.5 (Analytics Dashboard)
  All APIs -> US-7.x (Dashboard pages)
  US-4.x -> US-8.x (Webhooks)
```

---

## Grooming Committee Sign-Off

### Business Analyst
- All BRD user stories accounted for in the backlog.
- Added 8 infrastructure stories (US-0.1 through US-0.8) that were implicit in the BRD but not captured as stories.
- Acceptance criteria are sufficient for Sprint 1 and 2 stories. Sprint 3+ stories will need refinement before their sprint planning.
- **Key decision:** Dashboard (Epic 7) deferred entirely to Sprint 3+. API-first approach for MVP.
- **Key decision:** Bounce/complaint handling (Epic 6) moved to Sprint 2. Sprint 1 suppression check uses a manually seeded list if needed.
- **Missing from BRD:** Bootstrap mechanism for first API key (noted in US-5.1 grooming notes).

### Head of Design
- **Sprint 1:** Zero design work needed. All stories are API-only.
- **Sprint 2:** Zero design work needed. All stories are API-only.
- **Sprint 3+:** All 6 dashboard stories (US-7.1 through US-7.6) require wireframes and mockups before implementation. Flagged in each story card.
- **Design queue for Sprint 3 prep:** Start wireframes for US-7.1 (Overview) and US-7.2 (Log Viewer) during Sprint 2 so they are ready for Sprint 3 planning.
- **Component library recommendation:** MudBlazor for rapid Blazor UI development with built-in data tables, charts, and form components.

### Head of Engineering
- **Sprint 1 is tight at 45 points for 24 hours.** The critical path is US-0.1 -> US-0.2/0.3 -> US-5.1 -> US-3.1/3.2 -> US-1.1 -> US-1.3. Everything must be built sequentially. Parallel work is possible on infrastructure stories.
- **Highest risk story:** US-1.1 (8 points) -- end-to-end email send. This touches every component (API, RabbitMQ, Worker, SES, PostgreSQL). If this slips, the entire sprint fails.
- **Top 3 technical risks:** (1) SES sandbox mode blocking real sends, (2) RabbitMQ DLQ misconfiguration, (3) Time pressure on 24-hour sprint.
- **Recommendation:** Architect should design the shared abstractions (message contracts, repository interfaces, SES abstraction layer) before developers start individual stories.
- **Sprint 2 concern:** Open tracking (US-4.3) and click tracking (US-4.4) at 5 points each are complex. They require public-facing endpoints, HTML rewriting, and fast redirect handling. These are the riskiest Sprint 2 stories.

---

*This backlog is the output of the grooming ceremony and is ready for sprint planning.*
