# EaaS - Gate 1 Review: Architecture & Backlog

**Reviewer:** Staff Engineer
**Date:** 2026-03-27
**Documents Reviewed:** BRD_PRD.md v1.0, BACKLOG.md, ARCHITECTURE.md v1.0
**Sprint Under Review:** Sprint 1 (MVP)

---

## 1. Overall Assessment

**Verdict: APPROVE WITH CONDITIONS**

This is an impressively thorough architecture for a 24-hour MVP. The Architect has clearly thought through the system end-to-end: the DDL is copy-paste ready, the API contracts are complete with examples, the Docker Compose is production-configured with resource limits, and the message queue topology handles retry/DLQ correctly. The domain model is clean, the project structure follows solid .NET conventions, and the security posture (hashed API keys, least-privilege IAM, TLS, no secrets in code) is appropriate. However, there are several critical issues that will waste development time if not addressed before coding starts: the Sprint 1 scope is significantly overloaded for 24 hours, there is a contradiction between the backlog and architecture on what is in Sprint 1 vs Sprint 2, and the decision to use raw RabbitMQ.Client instead of MassTransit contradicts the backlog grooming notes and dramatically increases implementation complexity. These must be resolved before the developer begins.

---

## 2. Architecture Review

### 2.1 Solution Structure and Project Organization
**APPROVE**

The vertical slice approach in the API project (ADR-002) is the right call for this scale. Clean Architecture layering (Domain -> Infrastructure -> Api/Worker) is standard and correct. The Domain project has zero dependencies, which is proper. The Shared project is limited to cross-cutting concerns without business logic. One minor note: having both `EaaS.Shared` and `EaaS.Domain` could cause confusion about where DTOs live -- the Architecture doc is clear (Shared has API DTOs, Domain has entities), which is sufficient.

### 2.2 Database Schema (DDL)
**APPROVE WITH CONCERN**

Strengths:
- DDL is complete and copy-paste ready
- UUID primary keys throughout
- Proper audit columns (created_at, updated_at)
- Partial unique index on templates (handles soft delete correctly, and the self-correction from CONSTRAINT to partial index is good)
- Partitioned emails and email_events tables with 6 months of partitions pre-created
- Suppression list with proper unique constraint
- tenant_id future-proofing (ADR-007) is pragmatic

**CONCERN 1: Partitioning is over-engineering for Sprint 1.**
The emails and email_events tables use range partitioning by month. For an MVP handling <5K emails/month, this adds complexity without benefit. EF Core does not natively understand partitions, which means the developer must be careful with all queries to include `created_at` in any lookup by `id` (since the PK is composite: `id, created_at`). The `GetEmail` query handler must include `created_at` in the WHERE clause, but the API route is `GET /api/v1/emails/{message_id}` -- there is no `created_at` parameter.

**Fix:** For Sprint 1, use regular (non-partitioned) tables. Add partitioning in Sprint 2 or later when volume justifies it. The `idx_emails_message_id` unique index on `(message_id, created_at)` should be just on `(message_id)` for a non-partitioned table.

**CONCERN 2: email_events has no foreign key to emails.**
The `email_events` table references `email_id` but has no FK constraint. This is noted as being because both are partitioned -- correct, cross-partition FKs are not supported in PostgreSQL. But if we drop partitioning per Concern 1, we should add the FK.

**CONCERN 3: Missing `updated_at` on dns_records and suppression_list tables.**
The `dns_records` table has no `updated_at` column, which will be needed when verification status changes. The `suppression_list` table has `suppressed_at` but no `updated_at` -- acceptable since suppressions are append-only.

**CONCERN 4: Template versioning table is missing.**
The backlog (US-2.2) specifies "previous version retained (last 10 versions)" and the grooming notes mention a `TemplateVersion` table. The Architecture DDL has no `template_versions` table. The `templates` table has a `version` integer column but no mechanism to store previous versions. This is a gap.

**Fix:** Either (a) add a `template_versions` table to the DDL, or (b) explicitly defer template versioning to Sprint 2 and simplify US-2.2 acceptance criteria to just "update in place, increment version counter." Given the 24-hour constraint, option (b) is strongly recommended.

### 2.3 API Design
**APPROVE**

Excellent API design:
- Consistent response envelope (`{ success, data }` / `{ success, error }`)
- Well-defined error codes with HTTP status mapping
- Complete request/response schemas with examples
- Proper use of 202 Accepted for async operations
- Sensible validation rules documented per endpoint
- Pagination, filtering, and sorting on list endpoints

The API contract is detailed enough to implement from without questions.

### 2.4 Message Queue Design
**CONCERN**

The queue topology design is excellent -- the retry strategy with TTL-based delayed redelivery across three retry queues is a well-proven pattern. The DLQ routing is correct. Prefetch count and consumer configuration are sensible.

**CONCERN: Architecture specifies RabbitMQ.Client but Backlog says MassTransit.**
The backlog grooming notes for US-0.3 explicitly state: "MassTransit is the preferred abstraction -- it handles retry, DLQ, and consumer patterns out of the box for .NET." But the NuGet packages list only `RabbitMQ.Client` (version 6.8.1) with no MassTransit package. The Architecture file shows manual queue topology, manual retry routing, and a custom `RabbitMqConsumer` base class.

Using raw `RabbitMQ.Client` means the developer must manually implement:
- Exchange/queue declaration and binding
- Message serialization/deserialization
- Consumer lifecycle management
- Manual ack/nack with retry routing logic
- Connection recovery
- The entire DLX retry pattern described in section 5.1

This is a significant amount of plumbing code for a 24-hour sprint. MassTransit handles all of this declaratively.

**Fix:** Either (a) switch to MassTransit and simplify the infrastructure code (recommended -- saves 2-3 hours of implementation), or (b) if sticking with raw RabbitMQ.Client, acknowledge the additional implementation time and adjust sprint scope accordingly. If using MassTransit, the retry topology simplifies dramatically (MassTransit has built-in exponential retry with DLQ support via `UseMessageRetry` and `UseDelayedRedelivery`).

### 2.5 AWS SES Integration Approach
**APPROVE**

Sound approach:
- `SendRawEmail` is the correct choice for full MIME control
- IAM policy is properly scoped (least privilege)
- Domain verification flow is clearly documented
- SNS webhook processing with signature validation is correct
- The decision to use SES v2 SDK (`AWSSDK.SimpleEmailV2`) is appropriate

One note: the SES v2 SDK uses `SendEmailRequest` (not `SendRawEmail`), as the v2 API has a unified `SendEmail` endpoint that supports both simple and raw content. The IAM policy lists `ses:SendRawEmail` which is the v1 action. If using the v2 SDK, the IAM action should be `ses:SendEmail`. This is a minor fix but will cause a permissions error in production if missed.

**Fix:** Update IAM policy to include both `ses:SendRawEmail` and `ses:SendEmail`, or verify which SDK version (v1 vs v2) is actually being used and align the policy.

### 2.6 Docker Compose Configuration
**APPROVE**

This is well-configured:
- Resource limits on all containers (critical for 4GB VPS)
- Health checks on all infrastructure services
- Proper `depends_on` with `condition: service_healthy`
- Localhost-only port binding for infrastructure services
- Persistent volumes for data
- Nginx with TLS, security headers, WebSocket support for Blazor
- Certbot for automated certificate renewal
- RabbitMQ management UI for debugging

Total memory allocation: 512M (Postgres) + 192M (Redis) + 512M (RabbitMQ) + 256M (API) + 256M (Worker) + 128M (Webhook) + 192M (Dashboard) + 64M (Nginx) = **2,112MB**. This leaves ~1.9GB for the OS and buffer on a 4GB VPS. Tight but workable. Under load, the .NET services could exceed their limits and get OOM-killed.

**Minor concern:** The API container healthcheck uses `curl`, but the `.NET` Alpine base image does not include `curl` by default. The Dockerfile must explicitly install it, or use `wget` instead (which is available in Alpine).

**Fix:** Change API healthcheck to use `wget -q --spider http://localhost:8080/health || exit 1` or ensure the Dockerfile includes `curl`.

### 2.7 Configuration and Secrets Management
**APPROVE**

Appropriate for the deployment model. Environment variables via `.env` with `chmod 600` on the server is the right approach for a single-VPS, single-operator system. The `appsettings.json` has safe defaults with secrets left blank (overridden by environment). The IOptions pattern with startup validation catches missing config early.

### 2.8 NuGet Package Choices
**APPROVE WITH CONCERN**

Good choices overall:
- Fluid for Liquid rendering (fast, actively maintained)
- FluentValidation + MediatR pipeline (standard pattern)
- Serilog with compact JSON formatting
- StackExchange.Redis
- DnsClient for DNS verification
- BCrypt for dashboard password hashing
- MudBlazor for dashboard UI
- Testcontainers for integration tests

**CONCERN: AutoMapper is unnecessary overhead.**
For the vertical slice architecture with explicit request/response DTOs per feature, manual mapping in the handler (or simple extension methods) is cleaner and faster to implement than configuring AutoMapper profiles. AutoMapper adds a dependency, requires profile maintenance, and hides mapping logic. For ~16 endpoints, the mapping code is trivial.

**Recommendation:** Drop AutoMapper. Use manual mapping or a source generator like Mapperly if mapping boilerplate is a concern.

**CONCERN: MassTransit vs RabbitMQ.Client** (see section 2.4 above).

---

## 3. Backlog Review

### Are Sprint 1 stories correctly scoped for 24-hour MVP?

**NO -- Sprint 1 is overloaded.**

The backlog lists 14 stories at 45 points for Sprint 1. But the Architecture acceptance criteria (section 10) includes items that are NOT in Sprint 1 backlog:
- Batch sending (#10: `POST /api/v1/emails/batch`) -- this is US-1.2, assigned to Sprint 2 in the backlog
- Attachments (#11: email with PDF attachment) -- this is US-1.4, assigned to Sprint 2
- Bounce/complaint auto-suppression (#13, #14) -- this is US-6.1/US-6.2, assigned to Sprint 2
- Email list with filtering (#17: `GET /api/v1/emails`) -- not explicitly a Sprint 1 story
- Dashboard overview, login, log viewer (#19-#22) -- the Dashboard (Epic 7) is Sprint 3+ in the backlog

**This is a critical contradiction.** The Architect's acceptance criteria define a much larger Sprint 1 than the backlog. The developer will not know which to follow.

**Fix:** Align the backlog and architecture acceptance criteria. The backlog should be the source of truth. Remove acceptance criteria items 10, 11, 13, 14, 19-22 from Sprint 1, or move the corresponding stories into Sprint 1 (which would make it even more overloaded).

### Are story point estimates reasonable?

Mostly yes, with one exception:
- US-1.1 at 8 points is appropriate -- it is end-to-end across API, RabbitMQ, Worker, SES, and PostgreSQL
- US-0.1 at 5 points is reasonable for scaffolding + Docker Compose + Dockerfiles
- US-0.2 at 5 points is reasonable for schema + EF Core configuration + migrations

However, even the 45 points of genuine Sprint 1 stories are aggressive for 24 hours. With a single developer, that is roughly 3 points per hour with zero breaks. More realistic pacing is 2 points per hour, putting achievable scope at ~32-35 points.

### Are dependencies correctly identified?

Yes. The backlog correctly identifies US-0.1 (scaffolding) as the critical path blocker. The dependency chain is implicit but clear:
1. US-0.1 (scaffolding) -> everything
2. US-0.2 (database) -> all data-dependent stories
3. US-0.3 (Redis/RabbitMQ) -> US-1.1 (send email)
4. US-5.1 (API keys) -> US-1.1 (authentication required for sending)
5. US-3.1/3.2 (domains) -> US-1.1 (verified domain required for sending)
6. US-2.1 (templates) -> US-1.3 (template-based sending)

### Is anything missing that would block a working MVP?

Yes:
1. **First API key bootstrap problem.** How does the developer create the first API key? `POST /api/v1/keys` requires authentication via API key. This is a chicken-and-egg problem. The seed data script (`seed-data.sql`) is mentioned in the solution structure but not defined in the Architecture doc. The developer needs a way to create the initial API key -- either via seed data, a CLI command, or an unauthenticated bootstrap endpoint.

2. **Dashboard authentication bootstrap.** The `DASHBOARD_PASSWORD_HASH` must be generated before deployment. The Architecture should specify how to generate it (e.g., a script or CLI command that runs bcrypt).

### Is anything in Sprint 1 that should be deferred?

Yes:
- **US-2.2 (Update Template)** at 2 points could be deferred. Creating templates is essential for the MVP; updating them is a nice-to-have in the first 24 hours.
- **US-3.2 (Verify Domain DNS)** at 3 points. In Sprint 1, the developer can verify the domain manually through the SES console. The verification endpoint is nice but not strictly necessary if the domain is already verified in SES. However, this is debatable -- if the goal is a complete self-service API, it is needed.

---

## 4. Risk Assessment

### Technical Risks

1. **AWS SES Sandbox Mode.** A new SES account starts in sandbox mode, which only allows sending to verified email addresses. Production access requires a request that takes 1-3 business days. If the developer does not have production SES access, the MVP demo is limited to sending to pre-verified addresses. The backlog grooming notes mention this but do not treat it as a blocker. **It is a blocker for a true MVP demo.**

2. **EF Core + Partitioned Tables.** Even if partitioning is kept, EF Core's `FindAsync` and LINQ queries do not automatically include the partition key. Every query must explicitly include `created_at` in the WHERE clause, or PostgreSQL will scan all partitions. This is a performance trap that is easy to miss and hard to debug.

3. **RabbitMQ Quorum Queues on 4GB VPS.** The Architecture specifies quorum queues, which require Raft consensus and consume more memory than classic queues. With a single-node RabbitMQ instance, quorum queues provide no replication benefit -- they only add overhead. Classic mirrored queues (or just classic durable queues, since there is only one node) are more appropriate.

### Security Concerns

1. **WebhookProcessor is publicly accessible.** The nginx config routes `/webhooks/sns` to the webhook processor without authentication. The processor validates SNS signatures, which is correct, but the tracking endpoints (`/track/open/{token}`, `/track/click/{token}`) also run on this service. An attacker could brute-force tracking tokens to trigger false open/click events. The HMAC-signed tokens mitigate this, but the signing secret strength and token format should be documented.

2. **Dashboard authentication over the public internet.** The dashboard uses simple username/password auth with bcrypt. There is no rate limiting on login attempts, no account lockout, and no 2FA. For a single-user system this is acceptable, but adding a login rate limit (e.g., 5 attempts per minute) would be prudent.

3. **No CORS configuration specified.** If any client-side JavaScript will call the API, CORS headers are needed. If the API is strictly server-to-server, this is fine -- but it should be explicitly stated.

### Operational Risks

1. **No backup strategy defined for Sprint 1.** The BRD mentions daily PostgreSQL backups to S3, but the Architecture doc has no backup container, script, or cron job. For a 24-hour MVP this is acceptable, but it should be the first item in Sprint 2.

2. **No monitoring or alerting.** The health check endpoint exists, but there is no external monitoring (UptimeRobot, etc.) configured. If the VPS goes down, nobody is notified.

3. **Partition creation automation.** The DDL pre-creates 6 months of partitions. After that, inserts will fail. A background job or cron must create future partitions. This is not addressed in Sprint 1 scope.

### Performance Bottlenecks

1. **Nginx rate limiting is per-IP, not per-API-key.** The nginx config uses `$binary_remote_addr` for the rate limit zone, but the Architecture specifies per-API-key rate limiting. These are different things. A single client IP making requests with multiple API keys would be rate-limited by nginx before the application-level per-key rate limit kicks in. The two rate limiting layers should be documented as intentional (defense in depth) or the nginx layer should be relaxed for `/api/` paths.

2. **Storing full email body in RabbitMQ messages.** The Architecture justifies this to avoid DB roundtrips in the worker. At 100KB average and low volume this is fine. But base64-encoded attachments (up to 25MB) would also go through the queue, which could overwhelm RabbitMQ. The Architecture notes this risk for Sprint 2 (attachments) but does not address it. When attachments are added, the message schema must change to store attachment content elsewhere (filesystem or object storage) and pass only a reference.

---

## 5. Required Changes (Must Fix Before Development)

1. **Resolve Sprint 1 scope contradiction between Backlog and Architecture acceptance criteria.** The Architecture section 10 includes batch sending, attachments, bounce suppression, and dashboard as Sprint 1 acceptance criteria, but the Backlog assigns these to Sprint 2/3. Decision needed: which is the source of truth? Recommended: Backlog is the source of truth. Remove items 10, 11, 13, 14, 19-22 from Architecture section 10, or move them to a "Sprint 2 Acceptance Criteria" section.

2. **Decide MassTransit vs. raw RabbitMQ.Client and update both documents.** The Backlog says MassTransit; the Architecture specifies raw RabbitMQ.Client. Using MassTransit saves 2-3 hours and reduces bug surface. If MassTransit is chosen, update the NuGet packages and simplify the Infrastructure/Messaging code description. If raw client is chosen, update the Backlog grooming notes and acknowledge the additional implementation time.

3. **Remove table partitioning from Sprint 1 DDL.** Use regular tables for `emails` and `email_events`. This eliminates the composite PK problem, allows proper foreign keys, simplifies EF Core integration, and removes the partition-creation automation dependency. Add partitioning in a future sprint when volume justifies it.

4. **Define the first API key bootstrap mechanism.** Add either: (a) a seed SQL script that inserts a known API key hash for development, (b) a CLI command (`dotnet run -- create-key`), or (c) an admin-only bootstrap endpoint. The developer cannot test anything without an API key.

5. **Align SES SDK version with IAM policy.** If using `AWSSDK.SimpleEmailV2` (v2 SDK), verify the IAM actions match v2 API operations. Add `ses:SendEmail` to the IAM policy alongside `ses:SendRawEmail`.

6. **Switch from quorum queues to classic durable queues.** On a single-node RabbitMQ instance, quorum queues provide no benefit and consume more memory. Use classic durable queues for Sprint 1.

---

## 6. Recommendations (Nice-to-Have)

1. **Drop AutoMapper.** Manual mapping in each handler is simpler, faster to implement, and easier to debug for ~16 endpoints. One less dependency to configure.

2. **Defer template versioning (US-2.2 simplification).** Store templates in place with a version counter. Do not build the `TemplateVersion` table or history retrieval in Sprint 1. This saves 30-60 minutes.

3. **Add `updated_at` column to `dns_records` table.** Useful for tracking when verification status changed.

4. **Use `wget` instead of `curl` for container healthchecks.** Alpine images include `wget` but not `curl` by default.

5. **Add login rate limiting to the dashboard.** Simple middleware: 5 failed attempts per IP per minute. Prevents brute-force on the single admin account.

6. **Document CORS policy decision.** Explicitly state that the API is server-to-server only and CORS is not configured, or add permissive CORS for development.

7. **Consider Mapperly over AutoMapper** if you want zero-reflection mapping with source generators. But manual mapping is still the simplest option at this scale.

---

## 7. Sprint 1 Feasibility Check

### Is Sprint 1 scope achievable in 24 hours?

**Achievable but very tight, even after corrections.**

The 14 stories at 45 points, assuming the Backlog (not the Architecture acceptance criteria) is the source of truth, break down as:

| Priority | Stories | Points | Estimated Hours |
|----------|---------|--------|-----------------|
| P0 Critical Path | US-0.1, US-0.2, US-0.3, US-0.4, US-0.5, US-0.6 | 18 | 7-8h |
| P0 Core Feature | US-5.1, US-5.3, US-3.1, US-3.2, US-1.1 | 19 | 8-10h |
| P0 Template | US-2.1, US-2.2, US-1.3 | 8 | 3-4h |
| **Total** | **14 stories** | **45** | **18-22h** |

With breaks, context switching, debugging Docker issues, and AWS SES configuration, this is a ~22-hour effort. Possible but leaves no buffer.

### What to cut if time runs short (in priority order of what to drop first):

1. **US-2.2 (Update Template) -- 2 pts.** Templates can be created but not updated. Create a new one instead. Saves ~45 min.
2. **US-1.3 (Template-based Send) -- 3 pts.** Send with inline HTML only. Templates exist but are not yet wired to the send flow. Saves ~1.5h.
3. **US-2.1 (Create Template) -- 3 pts.** If US-1.3 is cut, templates are not needed in Sprint 1 at all. Saves ~1h.
4. **US-3.2 (Verify Domain DNS) -- 3 pts.** Verify the domain manually in SES console. The API endpoint is convenience, not necessity. Saves ~1.5h.
5. **US-5.3 (Revoke API Key) -- 2 pts.** Nice-to-have for security hygiene. Not critical for a solo-operator MVP. Saves ~30 min.

### Absolute minimum for a "working" MVP:

The irreducible core is:

| Story | Description | Points |
|-------|-------------|--------|
| US-0.1 | Scaffolding + Docker Compose | 5 |
| US-0.2 | Database schema + migrations | 5 |
| US-0.3 | Redis + RabbitMQ config | 3 |
| US-0.4 | Health check | 2 |
| US-0.5 | Structured logging | 2 |
| US-0.6 | Environment config | 1 |
| US-5.1 | Create API key | 3 |
| US-3.1 | Add domain (SES integration) | 3 |
| US-1.1 | Send a single email end-to-end | 8 |
| **Total** | | **32** |

This gives you: a working API that can create an API key, register a domain, and send an email that arrives in an inbox. That is a demonstrable MVP. Everything else is enhancement.

---

## Critical Questions Answered

1. **Can a developer implement Sprint 1 from the Architecture doc alone, without asking questions?**
Almost. The Architecture doc is unusually thorough. The two gaps are: (a) the first API key bootstrap problem, and (b) the Sprint 1 scope contradiction with the acceptance criteria. Fix those and the answer is yes.

2. **Is the database schema correct and complete for Sprint 1 features?**
Yes, with the caveat that partitioning should be removed and the template_versions table is missing (but should be deferred anyway).

3. **Will the Docker Compose actually work? Any missing dependencies?**
It will work with one fix: the API healthcheck uses `curl` which is not in Alpine images by default. Memory allocation is tight but feasible.

4. **Is the AWS SES integration approach sound?**
Yes. The domain verification flow, SNS webhook processing, and IAM policy are correct. The only issue is the v1/v2 SDK action name alignment.

5. **Are there any security vulnerabilities in the design?**
No critical vulnerabilities. The design follows security best practices (hashed keys, TLS, least-privilege IAM, SNS signature validation). Minor improvements: dashboard login rate limiting, explicit CORS policy.

6. **Will emails actually get delivered (not to spam)?**
Yes, if SPF/DKIM/DMARC are correctly configured (the Architecture provides exact DNS records). The main risk is SES sandbox mode limiting to verified addresses only until production access is granted.

---

**Review completed. Conditions in section 5 must be addressed before development begins. Estimated time to address conditions: 1-2 hours of document updates.**
