# SendNex - Business & Product Requirements Document

**Version:** 2.0
**Date:** 2026-04-12
**Author:** Senior Business Analyst
**Owner:** Israel Iyonsi
**Status:** Active

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Business Objectives](#2-business-objectives)
3. [Problem Statement](#3-problem-statement)
4. [Scope](#4-scope)
5. [Stakeholders](#5-stakeholders)
6. [Success Metrics](#6-success-metrics)
7. [Competitive Analysis](#7-competitive-analysis)
8. [Risk Assessment](#8-risk-assessment)
9. [Product Overview](#9-product-overview)
10. [User Personas](#10-user-personas)
11. [User Stories](#11-user-stories)
12. [Feature Prioritization](#12-feature-prioritization)
13. [Non-Functional Requirements](#13-non-functional-requirements)
14. [API Contract](#14-api-contract)
15. [Product Phases / Roadmap](#15-product-phases--roadmap)
16. [Constraints & Assumptions](#16-constraints--assumptions)
17. [Glossary](#17-glossary)

---

# PART I: BUSINESS REQUIREMENTS DOCUMENT (BRD)

---

## 1. Executive Summary

SendNex is a self-hosted, multi-tenant transactional email API platform that replaces third-party SaaS email providers (Resend, SendGrid, Postmark) with fully owned infrastructure. The platform provides an HTTP API for sending transactional emails (invoices, notifications, confirmations, password resets) with built-in template rendering, delivery tracking, bounce/complaint handling, scheduled sends, inbound email routing, and analytics.

The system runs on a single Hetzner CX22 VPS at sendnex.xyz, leverages AWS SES for SMTP delivery at $0.10 per 1,000 emails, and provides a Next.js 16 dashboard for monitoring and management. External tenants — including Eventra and CashTrack — are onboarded via a subscription billing model (Free, Starter, Pro, Business, Enterprise) with Paystack as the payment provider. Total operational cost is projected at $5-15/month for the platform operator.

SendNex is a production multi-tenant SaaS platform. It is not a personal-use tool. It does not include campaign management, mailing list management, drag-and-drop email editors, or marketing email functionality.

---

## 2. Business Objectives

| # | Objective | Measurable Target |
|---|-----------|-------------------|
| 1 | **Generate SaaS revenue** | Onboard paying tenants; achieve $200+/month MRR within 6 months of launch |
| 2 | **Reduce operator email infrastructure cost** | From $40+/month (Resend + SendGrid) to $5-15/month for platform operations (75-85% savings) |
| 3 | **Own the infrastructure** | Zero vendor lock-in; full control over data, uptime, and feature roadmap |
| 4 | **Serve external tenants** | Onboard Eventra and CashTrack as active paying tenants within Sprint 5 |
| 5 | **Gain operational visibility** | Real-time delivery analytics, bounce rates, open/click tracking, and admin platform analytics |
| 6 | **Build portfolio asset** | Demonstrate full-stack SaaS engineering capability (.NET 10, PostgreSQL, Redis, RabbitMQ, Docker, AWS SES, Paystack) |
| 7 | **Establish scalable SaaS foundation** | Multi-tenant architecture with per-tenant quota enforcement, billing, and admin governance |

---

## 3. Problem Statement

### Current State

Israel operates multiple applications (including CashTrack and Eventra) that send transactional emails to end-users. Each application historically integrated directly with one or more SaaS email providers. There is no unified email platform across these applications.

### Pain Points

1. **Recurring cost burden.** Resend charges $20/month for the Pro plan (50K emails). SendGrid charges $19.95/month for the Essentials plan. Combined spend exceeds $40/month for relatively low volume (<5K emails/month currently).

2. **Vendor lock-in.** Each provider has its own SDK, API format, template system, and dashboard. Switching providers requires code changes across every application.

3. **Fragmented visibility.** Delivery logs, bounce data, and analytics are scattered across multiple provider dashboards. There is no unified view of email health across all applications.

4. **Feature limitations on free/low tiers.** Free tiers impose daily sending limits, remove access to dedicated IPs, restrict template storage, and throttle API calls. Useful features like webhook notifications and advanced analytics sit behind higher pricing tiers.

5. **Data sovereignty concerns.** Email content, recipient lists, and delivery metadata are stored on third-party servers with no control over retention, access, or deletion policies.

6. **No customization.** Cannot modify bounce handling logic, retry strategies, rate limiting behavior, or template rendering pipelines. The provider decides how things work.

7. **No inbound email capability.** Existing SaaS providers do not provide inbound email routing, which is required for reply handling and automation use cases.

### Desired State

A single self-hosted multi-tenant SaaS API that all applications call to send transactional emails, with full ownership of the delivery pipeline, template system, analytics data, inbound routing, billing, and an operational admin panel.

---

## 4. Scope

### In Scope

- HTTP API for sending single, batch, and scheduled transactional emails
- Email template management with variable substitution (Liquid) and full version history with rollback
- File attachment support (up to 10MB per email, 25MB total)
- Domain verification management (SPF, DKIM, DMARC)
- Delivery tracking (sent, delivered, bounced, complained, opened, clicked)
- Email event history per message
- Bounce and complaint auto-suppression list with manual management
- API key management (create, revoke, rotate, scope per application)
- Webhook notifications to calling applications for delivery events, with delivery tracking and retry
- Inbound email receiving with configurable routing rules (webhook, forward, store)
- Scheduled email delivery (future-timestamp sends)
- Multi-tenant account system with isolated tenant data
- Subscription billing plans (Free, Starter, Pro, Business, Enterprise) via Paystack
- Invoice and billing management per tenant
- Tenant authentication (register, login)
- Admin panel with separate admin user authentication (HMAC-signed session cookies)
- Admin capabilities: tenant management, platform analytics, audit logging, system health
- Content review gate for flagged emails
- SSRF protection on webhook URLs
- Batch sending with per-tenant quota enforcement
- Next.js 16 dashboard for tenant monitoring and management
- Message queuing via RabbitMQ for reliable async delivery
- Redis caching for rate limiting, suppression list lookups, and template caching
- PostgreSQL for persistent storage of all platform data
- Docker Compose deployment on Hetzner CX22 VPS at sendnex.xyz
- AWS SES integration for SMTP delivery
- nginx reverse proxy with certbot TLS

### Out of Scope

- Marketing email campaigns or drip sequences
- Mailing list management or subscriber management
- Drag-and-drop email editor / visual email builder
- A/B testing of email content
- Contact/recipient CRM functionality
- SMS, push notification, or other non-email channels
- Custom SMTP server (we use AWS SES, not self-hosted Postfix/Sendmail)
- Mobile application
- Public-facing marketing website for the product

---

## 5. Stakeholders

| Stakeholder | Role | Interest |
|-------------|------|----------|
| **Israel Iyonsi** | Owner, developer, platform operator | Cost savings, infrastructure ownership, SaaS revenue, portfolio value |
| **Eventra** | External tenant | Reliable transactional email API for event management notifications |
| **CashTrack** | External tenant | Reliable delivery of invoices, payment confirmations, and notifications |
| **Tenant end-users** | Recipients of transactional emails | Reliable, timely delivery of invoices, notifications, and confirmations |
| **Future tenants** | Potential SaaS customers | Affordable, reliable transactional email API with good DX |

---

## 6. Success Metrics

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| **Monthly infrastructure cost** | < $15/month | AWS billing + Hetzner invoice |
| **Monthly Recurring Revenue (MRR)** | > $200/month within 6 months | Paystack billing dashboard |
| **Active paying tenants** | Eventra + CashTrack onboarded by end of Sprint 5 | Admin tenant dashboard |
| **Email delivery rate** | > 98% | Platform analytics (delivered / sent) |
| **Bounce rate** | < 2% | Platform analytics (bounced / sent) |
| **API response time (p95)** | < 200ms for single send | Application logs + dashboard |
| **API response time (p95)** | < 500ms for batch send (up to 100) | Application logs + dashboard |
| **System uptime** | > 99.5% (< 3.65 hours downtime/month) | Health check monitoring |
| **Time to integrate new tenant** | < 30 minutes | Developer experience measurement |
| **Template render time (p95)** | < 50ms | Application logs |
| **Suppression list accuracy** | 100% of hard bounces auto-suppressed | Log audit |

---

## 7. Competitive Analysis

### Pricing Comparison

| Feature | **SendNex (Self-Hosted)** | **Resend** | **SendGrid** | **Postmark** | **Mailgun** |
|---------|--------------------------|------------|-------------|-------------|-------------|
| **Monthly base cost** | ~$5-15 (VPS + SES) | $0 (free) / $20 (Pro) | $0 (free) / $19.95 (Essentials) | $15 (10K emails) | $0 (free) / $35 (Foundation) |
| **Free tier emails** | Yes (Free plan quota) | 3,000/month | 100/day | 100/month | 1,000/month |
| **Cost per 1K emails** | $0.10 (SES) + plan fee | $0.00-$0.40 | $0.00-$0.50 | $1.25-$1.50 | $0.80-$1.00 |
| **Cost at 5K emails/month** | Free plan | $0 (free tier) | $0 (free tier) | $15 | $0 (free tier) |
| **Cost at 50K emails/month** | ~$9.35 + plan | $20 | $19.95 | $50 | $35 |
| **Cost at 200K emails/month** | ~$24.35 + plan | $80 | $49.95 | $155 | $75 |
| **Data ownership** | Full | None | None | None | None |
| **Vendor lock-in** | None | High | High | High | High |

### Feature Comparison

| Feature | **SendNex** | **Resend** | **SendGrid** | **Postmark** | **Mailgun** |
|---------|------------|------------|-------------|-------------|-------------|
| **REST API** | Yes | Yes | Yes | Yes | Yes |
| **SMTP relay** | No (API only) | Yes | Yes | Yes | Yes |
| **Template management** | Yes + versioning + rollback | Yes | Yes (Dynamic Templates) | Yes | Yes |
| **Template rendering** | Liquid | React Email | Handlebars | Mustachio | Handlebars |
| **Batch sending** | Yes + quota enforcement | Yes | Yes | Yes | Yes |
| **Scheduled sends** | Yes | No | No | No | No |
| **Inbound email routing** | Yes | No | No | No | Yes |
| **Attachments** | Yes | Yes | Yes | Yes | Yes |
| **Open tracking** | Yes | Yes | Yes | Yes | Yes |
| **Click tracking** | Yes | Yes | Yes | Yes | Yes |
| **Bounce handling** | Auto-suppression | Auto | Auto | Auto | Auto |
| **Webhooks** | Yes + delivery tracking | Yes | Yes | Yes | Yes |
| **Analytics dashboard** | Yes (Next.js 16) | Yes | Yes | Yes | Yes |
| **Domain verification** | Yes | Yes | Yes | Yes | Yes |
| **Multi-tenant** | Yes (native) | No | No | No | No |
| **Subscription billing** | Yes (Paystack) | N/A | N/A | N/A | N/A |
| **Admin panel** | Yes (separate auth) | No | No | No | No |
| **Dedicated IP** | No (SES shared) | Paid add-on | Paid add-on | Included (some plans) | Paid add-on |
| **Custom retry logic** | Full control | No | No | No | No |
| **Self-hosted** | Yes | No | No | No | No |

### Competitive Advantages of SendNex

1. **Cost efficiency at any volume.** At current volumes, cost is dramatically lower than SaaS alternatives. At scale (50K+/month), cost remains the lowest in class.
2. **Full data ownership.** All email content, metadata, and analytics stored on owned infrastructure.
3. **Zero vendor lock-in.** Tenants integrate with a stable API. The underlying delivery provider (SES) can be swapped without touching tenant code.
4. **Native multi-tenancy.** Built from the ground up for multiple isolated tenants with billing, quotas, and admin governance.
5. **Inbound email routing.** Unique capability vs. most transactional email SaaS providers.
6. **Scheduled sends.** Native support for future-timestamp email delivery.
7. **Custom logic.** Full control over retry policies, rate limiting, bounce handling, and template rendering.
8. **Portfolio value.** Demonstrates production-grade SaaS infrastructure engineering.

### Competitive Disadvantages

1. **Maintenance burden.** No vendor support; Israel is responsible for uptime, security patches, and bug fixes.
2. **No dedicated IP.** AWS SES uses shared IP pools (dedicated IP available at $24.95/month, not worth it for current volume).
3. **No SMTP relay.** API-only; applications that require SMTP integration would need adaptation.
4. **Single point of failure.** Single VPS; no geographic redundancy without additional infrastructure.

---

## 8. Risk Assessment

| # | Risk | Likelihood | Impact | Mitigation |
|---|------|-----------|--------|------------|
| 1 | **Email deliverability issues** (spam folder, blocked by ISPs) | Medium | High | Properly configure SPF, DKIM, DMARC. Monitor sender reputation via AWS SES dashboard. Enforce suppression list. Start with low volume and warm up. |
| 2 | **AWS SES account suspension** (complaint rate exceeds threshold) | Low | Critical | Implement hard bounce auto-suppression. Monitor complaint rate (must stay below 0.1%). Set up SES sending quotas and alarms. Content review gate for flagged emails. |
| 3 | **VPS downtime** (hardware failure, network outage) | Low | High | RabbitMQ persistence ensures messages survive restarts. Implement health checks with external monitoring (UptimeRobot). Daily PostgreSQL backups to S3. |
| 4 | **Maintenance burden exceeds expectations** | Medium | Medium | Keep architecture simple. Use battle-tested libraries. Automate deployments with Docker Compose. Document everything. |
| 5 | **Security breach** (API key leaked, SSRF attack on webhooks) | Low | Critical | API key hashing (never store plain text). Rate limiting per key. HTTPS only. SSRF protection on webhook URLs. Regular dependency updates. |
| 6 | **Tenant quota abuse** (runaway sends, infinite loop in calling app) | Low | Medium | Per-tenant rate limits and subscription quota enforcement. Daily sending quota per API key. Budget alerts on AWS. Circuit breaker in the worker service. |
| 7 | **Data loss** (PostgreSQL corruption) | Low | High | Automated daily backups. Point-in-time recovery with WAL archiving. Backup verification script. |
| 8 | **Scaling limits** (single VPS cannot handle tenant load) | Low | Medium | Current VPS handles 10K+ emails/day easily. If needed, scale vertically or add worker nodes. RabbitMQ supports distributed consumers. |
| 9 | **SES region outage** | Very Low | High | Configure SES in a secondary region as fallback. Implement provider abstraction layer for easy switching. |
| 10 | **Tenant payment failures** (Paystack subscription lapse) | Medium | Medium | Automated subscription expiry checks. Grace period before quota enforcement. Clear billing alerts via dashboard. |

---

# PART II: PRODUCT REQUIREMENTS DOCUMENT (PRD)

---

## 9. Product Overview

SendNex is a self-hosted multi-tenant transactional email platform consisting of three core components:

1. **API Server** (.NET 10 Minimal API) - Accepts email send requests, manages templates/domains/API keys/webhooks/inbound rules/billing, and serves the dashboard.
2. **Worker Service** (.NET 10 Worker Service) - Consumes messages from RabbitMQ, renders templates, delivers emails via AWS SES, processes delivery status webhooks from SES, executes scheduled sends, and dispatches tenant webhook notifications.
3. **Dashboard** (Next.js 16) - Web UI for tenants to view send logs, analytics, manage templates, domains, API keys, inbound rules, suppressions, and billing. Separate admin panel for platform governance.

### Architecture Overview

```
[Tenant App] --HTTP POST (Bearer API Key)--> [API Server] --enqueue--> [RabbitMQ]
                                                  |                         |
                                                  |                    [Worker Service]
                                                  |                         |
                                              [PostgreSQL]           [AWS SES] --SMTP--> [Recipient]
                                              [Redis]                    |
                                                  |                 [SNS Webhook]
                                           [Next.js Dashboard]          |
                                           [Admin Panel]           [Worker Service] --update--> [PostgreSQL]
                                                  |
                                            [Paystack Billing]
```

### Infrastructure

| Component | Detail |
|-----------|--------|
| **Hosting** | Hetzner CX22 VPS — 2 vCPU, 4GB RAM, 40GB SSD |
| **Domain** | sendnex.xyz |
| **TLS** | certbot (Let's Encrypt) |
| **Reverse proxy** | nginx |
| **Deployment** | Docker Compose |

### Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| API Server | .NET 10 Minimal API | HTTP API, authentication, request validation |
| Worker Service | .NET 10 Worker Service | Queue consumption, template rendering, SES delivery, scheduled sends |
| Dashboard | Next.js 16 | Tenant management UI, analytics, log viewer |
| Admin Panel | Next.js 16 (separate auth) | Platform governance, tenant management, audit logs |
| Database | PostgreSQL 16 | Persistent storage (logs, templates, domains, keys, tenants, billing) |
| Cache | Redis 7 | Rate limiting, suppression list cache, template cache |
| Message Queue | RabbitMQ 3.13 | Async email processing, retry with dead-letter queues |
| Email Delivery | AWS SES | SMTP delivery, bounce/complaint notifications via SNS |
| Payment | Paystack | Subscription billing and invoicing |
| Containerization | Docker Compose | Single-command deployment |
| Hosting | Hetzner VPS (CX22) | 2 vCPU, 4GB RAM, 40GB SSD, ~$4.35/month |

---

## 10. User Personas

### Persona 1: Israel (Owner / Platform Operator)

| Attribute | Detail |
|-----------|--------|
| **Name** | Israel Iyonsi |
| **Role** | Senior .NET Engineer, platform operator, admin |
| **Technical skill** | Expert - 8+ years backend development |
| **Goals** | Run a stable multi-tenant SaaS, generate revenue, own infrastructure |
| **Frustrations** | Platform stability, tenant onboarding friction, billing edge cases |
| **Usage pattern** | Manages platform via admin panel. Monitors tenant health, audit logs, platform analytics. Reviews flagged content. |
| **Devices** | Desktop browser (Chrome/Edge), no mobile requirement |

### Persona 2: Tenant Developer (e.g., Eventra, CashTrack)

| Attribute | Detail |
|-----------|--------|
| **Name** | Tenant Application Developer |
| **Role** | Developer integrating SendNex into their product |
| **Integration method** | HTTP POST to SendNex API with API key authentication |
| **Usage pattern** | Sends 50-5,000 emails/day across invoice notifications, payment confirmations, event reminders, user alerts |
| **Requirements** | Reliable delivery, fast API response, template rendering, scheduled sends, inbound routing, delivery status callbacks |
| **Subscription** | Free → Starter → Pro based on volume |

### Persona 3: Tenant Admin / Non-Technical User

| Attribute | Detail |
|-----------|--------|
| **Name** | Tenant Business User |
| **Role** | Monitors email delivery, manages billing, views analytics |
| **Technical skill** | Low-intermediate |
| **Goals** | Confirm emails are delivered, manage subscription, download invoices |
| **Usage pattern** | Logs into dashboard weekly. Checks delivery rates and recent sends. Manages billing subscription. |

---

## 11. User Stories

### Epic 1: Email Sending

#### US-1.1: Send a Single Email

**As a** developer integrating with SendNex,
**I want** to send a single transactional email via the API,
**so that** my application can notify users without managing SMTP directly.

**Acceptance Criteria:**

1. API accepts POST `/api/v1/emails/send` with `to`, `from`, `subject`, `html_body` (and optional `text_body`).
2. API validates all required fields and returns 400 with descriptive errors for invalid input.
3. API returns 202 Accepted with a unique `message_id` within 200ms (p95).
4. The email is enqueued to RabbitMQ for async processing.
5. The worker picks up the message, delivers via SES, and logs the result in PostgreSQL.
6. If the recipient is on the suppression list, the API returns 422 with a clear error message.
7. The `from` address must belong to a verified domain.
8. Send is rejected if tenant's subscription quota is exhausted.

#### US-1.2: Send a Batch of Emails

**As a** developer,
**I want** to send up to 100 emails in a single API call,
**so that** I can efficiently send bulk transactional emails (e.g., monthly invoice reminders).

**Acceptance Criteria:**

1. API accepts POST `/api/v1/emails/batch` with an array of email objects (max 100).
2. Each email in the batch is validated independently; partial failures are reported per-item.
3. API returns 202 Accepted with an array of `message_id` values within 500ms (p95).
4. All emails are enqueued individually to RabbitMQ for independent processing.
5. A `batch_id` is returned for querying the status of all emails in the batch.
6. Suppressed recipients are skipped with per-item error detail.
7. Batch is rejected if it would exceed tenant quota; partial acceptance is not permitted — the full batch must fit within quota.

#### US-1.3: Send an Email Using a Template

**As a** developer,
**I want** to send an email by referencing a stored template and passing variables,
**so that** I don't have to embed HTML in my API calls.

**Acceptance Criteria:**

1. API accepts POST `/api/v1/emails/send` with `template_id` and `variables` object instead of `html_body`.
2. The worker renders the template with the provided variables before sending.
3. If `template_id` does not exist, API returns 404 with error detail.
4. If required template variables are missing, API returns 400 listing the missing variables.
5. Template rendering errors are logged and the email is moved to the dead-letter queue.

#### US-1.4: Send an Email with Attachments

**As a** developer,
**I want** to attach files to transactional emails,
**so that** I can send invoices, receipts, and documents to users.

**Acceptance Criteria:**

1. API accepts `attachments` array with each item containing `filename`, `content` (base64-encoded), and `content_type`.
2. Individual attachment size limit: 10MB.
3. Total attachment size per email: 25MB.
4. API returns 400 if attachment size limits are exceeded.
5. Attachments are passed through to SES and delivered with the email.
6. Supported content types include: `application/pdf`, `image/png`, `image/jpeg`, `text/csv`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

#### US-1.5: Send an Email with CC and BCC

**As a** developer,
**I want** to include CC and BCC recipients on transactional emails,
**so that** I can copy relevant parties on notifications.

**Acceptance Criteria:**

1. API accepts optional `cc` and `bcc` arrays of email addresses.
2. CC recipients are visible in the email headers; BCC recipients are not.
3. Each CC/BCC address is validated for format.
4. Suppressed addresses in CC/BCC are silently removed (not sent, logged as skipped).
5. Combined total of `to` + `cc` + `bcc` must not exceed 50 recipients per email.

#### US-1.6: Schedule an Email for Future Delivery

**As a** developer,
**I want** to schedule an email to be sent at a specific future timestamp,
**so that** I can time notifications to arrive at optimal moments.

**Acceptance Criteria:**

1. API accepts `scheduled_at` (ISO 8601 UTC timestamp) on the send request.
2. Scheduled emails are stored with status `scheduled` and not enqueued until the scheduled time.
3. The worker checks for due scheduled emails at regular intervals (every 30 seconds).
4. Scheduled emails support cancellation via DELETE before the scheduled time.
5. Scheduled time must be at least 60 seconds in the future; otherwise API returns 400.
6. Quota is reserved at scheduling time, not at delivery time.

#### US-1.7: Get Email Event History

**As a** developer,
**I want** to retrieve the full delivery event history for a specific email,
**so that** I can trace exactly what happened to a message.

**Acceptance Criteria:**

1. GET `/api/v1/emails/{message_id}/events` returns an ordered list of all events for the email.
2. Events include: `queued`, `sent`, `delivered`, `opened`, `clicked`, `bounced`, `complained`, `failed`.
3. Each event includes: `event_type`, `timestamp`, `metadata` (e.g., bounce reason, click URL).
4. Returns 404 if `message_id` does not belong to the authenticated tenant.

---

### Epic 2: Template Management

#### US-2.1: Create an Email Template

**As a** developer,
**I want** to create and store email templates with variable placeholders,
**so that** I can reuse them across multiple email sends.

**Acceptance Criteria:**

1. API accepts POST `/api/v1/templates` with `name`, `subject_template`, `html_body`, `text_body` (optional), and `variables_schema` (JSON Schema for expected variables).
2. Template names must be unique per tenant.
3. Template is validated for syntax errors (Liquid template syntax).
4. Template is stored in PostgreSQL and cached in Redis.
5. API returns 201 Created with the `template_id`.
6. Maximum template size: 512KB.

#### US-2.2: Update an Email Template (with Versioning)

**As a** developer,
**I want** to update an existing template with full version history,
**so that** I can iterate on email content while retaining the ability to rollback.

**Acceptance Criteria:**

1. API accepts PUT `/api/v1/templates/{id}` with updatable fields.
2. Every update creates a new version; previous versions are retained indefinitely.
3. Redis cache is invalidated on update.
4. API returns 200 OK with the updated template and new version number.
5. Active sends using the old version complete with the old content (no mid-send changes).

#### US-2.3: List and Retrieve Templates

**As a** developer,
**I want** to list all templates and retrieve a specific template by ID,
**so that** I can manage my template library.

**Acceptance Criteria:**

1. GET `/api/v1/templates` returns paginated list (default 20, max 100 per page).
2. GET `/api/v1/templates/{id}` returns the full template including body and variables schema.
3. List endpoint supports filtering by `name` (partial match) and sorting by `created_at` or `updated_at`.
4. Response includes `version`, `created_at`, `updated_at`, and `send_count` metadata.

#### US-2.4: Delete an Email Template

**As a** developer,
**I want** to delete a template that is no longer needed,
**so that** I can keep my template library clean.

**Acceptance Criteria:**

1. DELETE `/api/v1/templates/{id}` soft-deletes the template (sets `deleted_at`).
2. Soft-deleted templates are excluded from list results.
3. Sends referencing a deleted `template_id` return 404.
4. Redis cache entry is removed.
5. Soft-deleted templates can be restored within 30 days via PATCH `/api/v1/templates/{id}/restore`.

#### US-2.5: Preview a Template

**As a** developer,
**I want** to preview a rendered template with sample variables,
**so that** I can verify the output before sending.

**Acceptance Criteria:**

1. POST `/api/v1/templates/{id}/preview` accepts a `variables` object.
2. Returns the rendered HTML and text bodies without sending any email.
3. Missing variables are highlighted in the rendered output (not silently omitted).
4. Response includes the rendered subject line.

#### US-2.6: List Template Versions

**As a** developer,
**I want** to view all versions of a template,
**so that** I can audit changes and choose a version to rollback to.

**Acceptance Criteria:**

1. GET `/api/v1/templates/{id}/versions` returns all saved versions with version number, created_at, and a summary of changes.
2. Each version entry includes the full `html_body`, `text_body`, and `subject_template`.

#### US-2.7: Rollback a Template to a Previous Version

**As a** developer,
**I want** to rollback a template to a previous version,
**so that** I can quickly recover from a bad template update.

**Acceptance Criteria:**

1. POST `/api/v1/templates/{id}/rollback` with `version` number sets the active template to the specified version.
2. Rollback creates a new version entry (the rolled-back content becomes the latest version).
3. Redis cache is invalidated immediately.
4. Response returns the new current version number.

---

### Epic 3: Domain Management

#### US-3.1: Add a Sending Domain

**As a** developer,
**I want** to register a domain for sending emails,
**so that** I can send from addresses on that domain.

**Acceptance Criteria:**

1. POST `/api/v1/domains` accepts a `domain` name (e.g., `notifications.cashtrack.app`).
2. System generates the required DNS records: SPF TXT record, DKIM CNAME records (3 entries for SES), DMARC TXT record.
3. API returns the DNS records that must be configured by the user.
4. Domain status is set to `pending_verification`.
5. Domain name is validated for format.
6. Duplicate domain names within the same tenant are rejected with 409 Conflict.

#### US-3.2: Verify Domain DNS Configuration

**As a** developer,
**I want** to trigger domain verification after configuring DNS records,
**so that** I can start sending from that domain.

**Acceptance Criteria:**

1. POST `/api/v1/domains/{id}/verify` triggers DNS record verification.
2. System checks SPF, DKIM, and DMARC records against expected values.
3. Each record type is independently reported as `verified` or `failed` with the expected vs. actual values.
4. If all records pass, domain status changes to `verified`.
5. Automatic re-verification runs daily for all verified domains (detect DNS changes).
6. Domain becomes `suspended` if verification fails on re-check (with notification via dashboard).

#### US-3.3: List and View Domains

**As a** developer,
**I want** to list all registered domains and their verification status,
**so that** I can manage my sending domains.

**Acceptance Criteria:**

1. GET `/api/v1/domains` returns all tenant domains with status, DNS records, and verification timestamps.
2. GET `/api/v1/domains/{id}` returns full domain detail including individual DNS record statuses.
3. Response includes `last_verified_at` timestamp and next scheduled verification time.

#### US-3.4: Remove a Sending Domain

**As a** developer,
**I want** to remove a domain that I no longer use,
**so that** I can keep my domain list clean.

**Acceptance Criteria:**

1. DELETE `/api/v1/domains/{id}` removes the domain.
2. Emails in queue for that domain are rejected with a clear error.
3. Future sends from that domain are rejected with 400.
4. SES domain identity is removed.

---

### Epic 4: Analytics and Logging

#### US-4.1: View Email Send Logs

**As a** developer,
**I want** to view a log of all sent emails with their delivery status,
**so that** I can troubleshoot delivery issues.

**Acceptance Criteria:**

1. GET `/api/v1/emails` returns paginated email logs with: `message_id`, `to`, `from`, `subject`, `status`, `sent_at`, `delivered_at`, `opened_at`, `clicked_at`.
2. Supports filtering by: `status`, `to`, `from`, `date_range`, `api_key_id`, `template_id`.
3. Supports sorting by `sent_at` (default desc).
4. Default pagination: 50 items, max 200 per page.
5. GET `/api/v1/emails/{message_id}` returns full email detail.
6. Logs are retained for 90 days (configurable).

#### US-4.2: View Outbound Delivery Analytics

**As a** developer,
**I want** to see aggregate outbound delivery statistics,
**so that** I can monitor the health of my email sending.

**Acceptance Criteria:**

1. GET `/api/v1/analytics/outbound/summary` returns aggregate stats for a given date range.
2. Metrics include: `total_sent`, `total_delivered`, `total_bounced`, `total_complained`, `total_opened`, `total_clicked`, `delivery_rate`, `open_rate`, `click_rate`, `bounce_rate`, `complaint_rate`.
3. GET `/api/v1/analytics/outbound/timeline` returns time-series data grouped by day/week/month.
4. Supports filtering by: `domain`, `api_key_id`, `template_id`.
5. Default date range: last 30 days.

#### US-4.3: View Inbound Analytics

**As a** developer,
**I want** to see summary statistics for inbound emails received,
**so that** I can understand inbound volume and routing performance.

**Acceptance Criteria:**

1. GET `/api/v1/analytics/inbound/summary` returns: `total_received`, `total_routed`, `total_failed_routing`, by date range.
2. Breakdowns by inbound rule are included.

#### US-4.4: Track Email Opens

**As a** developer,
**I want** to know when recipients open my emails,
**so that** I can understand engagement.

**Acceptance Criteria:**

1. Open tracking is enabled by default (configurable per-send via `track_opens: false`).
2. A 1x1 transparent tracking pixel is injected into the HTML body.
3. When the pixel is loaded, an `opened` event is recorded with timestamp and approximate geolocation (country level from IP).
4. Multiple opens are recorded but `first_opened_at` is tracked separately.
5. Open tracking is only applied to HTML emails (not text-only).

#### US-4.5: Track Link Clicks

**As a** developer,
**I want** to know when recipients click links in my emails,
**so that** I can understand which content drives action.

**Acceptance Criteria:**

1. Click tracking is enabled by default (configurable per-send via `track_clicks: false`).
2. Links in the HTML body are rewritten to pass through the SendNex tracking endpoint.
3. When a link is clicked, a `clicked` event is recorded with the original URL, timestamp, and user agent.
4. The user is immediately redirected to the original URL (< 100ms redirect time).
5. Multiple clicks on the same link are recorded individually.
6. Unsubscribe links (containing `unsubscribe` in the URL) are never rewritten.

---

### Epic 5: API Key Management

#### US-5.1: Create an API Key

**As a** developer,
**I want** to create API keys scoped to specific applications,
**so that** I can track sending per-app and revoke access independently.

**Acceptance Criteria:**

1. POST `/api/v1/api-keys` accepts `name` (e.g., "CashTrack Production") and optional `allowed_domains`.
2. The full API key is returned only once at creation time. It is stored as a SHA-256 hash.
3. API key format: `snx_live_` prefix + 40 random alphanumeric characters.
4. Key is immediately active upon creation.
5. Response includes `key_id`, `name`, `created_at`, and `prefix` (first 8 chars for identification).

#### US-5.2: List and View API Keys

**As a** developer,
**I want** to list all API keys with their metadata,
**so that** I can manage application access.

**Acceptance Criteria:**

1. GET `/api/v1/api-keys` returns all keys with: `key_id`, `name`, `prefix`, `created_at`, `last_used_at`, `status`, `send_count`.
2. The full key value is never returned after creation.
3. Response includes usage statistics per key.

#### US-5.3: Revoke an API Key

**As a** developer,
**I want** to revoke an API key that is compromised or no longer needed,
**so that** I can prevent unauthorized access.

**Acceptance Criteria:**

1. DELETE `/api/v1/api-keys/{id}` immediately revokes the key.
2. Revoked keys return 401 Unauthorized on any subsequent API call.
3. Emails already in queue from the revoked key continue to process (they were already authorized).
4. Revocation is logged with timestamp and reason (optional).

#### US-5.4: Rotate an API Key

**As a** developer,
**I want** to rotate an API key without downtime,
**so that** I can maintain security hygiene.

**Acceptance Criteria:**

1. POST `/api/v1/api-keys/{id}/rotate` generates a new key and returns it.
2. The old key remains valid for a configurable grace period (default: 24 hours).
3. After the grace period, the old key is automatically revoked.
4. Both old and new keys work during the grace period.
5. The new key inherits all permissions from the old key.

---

### Epic 6: Bounce and Complaint Handling

#### US-6.1: Automatic Bounce Suppression

**As a** developer,
**I want** hard-bounced email addresses to be automatically suppressed,
**so that** I don't hurt my sender reputation by repeatedly sending to invalid addresses.

**Acceptance Criteria:**

1. When AWS SES reports a hard bounce (permanent failure), the recipient address is added to the suppression list.
2. Soft bounces (temporary failure) trigger up to 3 retries with exponential backoff (1 min, 5 min, 30 min).
3. After 3 consecutive soft bounces for the same address, it is added to the suppression list.
4. Suppressed addresses are cached in Redis for O(1) lookup at send time.
5. Emails to suppressed addresses are rejected at the API level (never enqueued).

#### US-6.2: Automatic Complaint Suppression

**As a** developer,
**I want** addresses that file spam complaints to be automatically suppressed,
**so that** I maintain a complaint rate below AWS SES thresholds.

**Acceptance Criteria:**

1. When AWS SES reports a complaint (via SNS), the address is immediately suppressed.
2. Complaint events are logged with the complaint feedback type (abuse, not-spam, etc.).
3. If complaint rate exceeds 0.08%, a warning is logged and surfaced on the dashboard.
4. If complaint rate exceeds 0.1%, sending is paused with a dashboard alert (SES suspension threshold).

#### US-6.3: Manage Suppression List

**As a** developer,
**I want** to view and manage the suppression list,
**so that** I can manually add or remove addresses when needed.

**Acceptance Criteria:**

1. GET `/api/v1/suppressions` returns paginated list with: `email`, `reason` (hard_bounce, soft_bounce_limit, complaint, manual), `suppressed_at`, `source_message_id`.
2. POST `/api/v1/suppressions` allows manually adding an address.
3. DELETE `/api/v1/suppressions/{email}` removes an address (re-enables sending).
4. Removing a hard-bounced or complained address triggers a confirmation warning.
5. Suppression list changes are audit-logged.

---

### Epic 7: Dashboard

#### US-7.1: Dashboard Overview

**As a** tenant user,
**I want** a dashboard home page showing key metrics at a glance,
**so that** I can quickly assess the health of my email sending.

**Acceptance Criteria:**

1. Dashboard home shows: total sent (today, 7d, 30d), delivery rate, bounce rate, complaint rate, open rate, click rate.
2. A line chart shows daily send volume for the last 30 days.
3. A status indicator shows: system health (API up/down, worker up/down, queue depth).
4. Recent alerts are shown (high bounce rate, complaint threshold, domain verification failures).
5. Page loads in under 2 seconds.

#### US-7.2: Email Log Viewer

**As a** tenant user,
**I want** to browse and search email logs in the dashboard,
**so that** I can troubleshoot specific emails without using the API.

**Acceptance Criteria:**

1. Dashboard shows a searchable, filterable table of email logs.
2. Filters: status, date range, recipient, sender, template, API key.
3. Clicking a row opens the full detail view: all status transitions, timestamps, headers, template used, variables passed.
4. Supports text search across subject and recipient.
5. Table supports column sorting.
6. Paginated with 50 rows per page.

#### US-7.3: Template Manager

**As a** tenant user,
**I want** to manage email templates through the dashboard,
**so that** I can create and edit templates without making API calls.

**Acceptance Criteria:**

1. Dashboard lists all templates with name, version, send count, last updated.
2. Template editor with syntax highlighting for Liquid/HTML.
3. Live preview panel that renders the template with sample variables.
4. Variable schema editor (define expected variables and their types).
5. Version history viewer with rollback support.

#### US-7.4: Domain Manager

**As a** tenant user,
**I want** to manage sending domains through the dashboard,
**so that** I can add domains and check verification status visually.

**Acceptance Criteria:**

1. Dashboard lists all domains with verification status (color-coded: green=verified, yellow=pending, red=failed).
2. Adding a domain shows the required DNS records in a copyable format.
3. A "Verify Now" button triggers immediate verification.
4. Individual DNS record status is shown (SPF, DKIM, DMARC independently).

#### US-7.5: Analytics Dashboard

**As a** tenant user,
**I want** rich analytics charts in the dashboard,
**so that** I can visualize sending trends and email performance.

**Acceptance Criteria:**

1. Time-series chart: daily sends, deliveries, bounces, complaints over selected date range.
2. Pie/donut chart: delivery status breakdown.
3. Bar chart: top sending templates by volume.
4. Bar chart: top sending domains by volume.
5. Table: per-API-key sending statistics.
6. Inbound email volume chart.
7. Date range selector with presets: today, 7d, 30d, 90d, custom.
8. All charts are interactive (hover for values, click to drill down).

#### US-7.6: Suppression List Manager

**As a** tenant user,
**I want** to manage the suppression list through the dashboard,
**so that** I can review and modify suppressed addresses visually.

**Acceptance Criteria:**

1. Dashboard shows suppressed addresses with reason, date, and source email.
2. Search by email address (partial match).
3. Bulk remove selected addresses.
4. Manual add with reason selection.
5. Export suppression list as CSV.

#### US-7.7: Billing and Subscription Management

**As a** tenant user,
**I want** to manage my subscription plan and view invoices through the dashboard,
**so that** I can control my SendNex spend.

**Acceptance Criteria:**

1. Dashboard shows current plan, quota usage (emails remaining this period), and renewal date.
2. Tenant can browse available plans and upgrade/downgrade.
3. Subscription changes are processed via Paystack.
4. Invoice history is displayed with download links.
5. Cancellation flow is available with immediate confirmation.

---

### Epic 8: Webhook Notifications

#### US-8.1: Configure Webhook Endpoints

**As a** developer,
**I want** to register webhook URLs that receive delivery event notifications,
**so that** my applications can react to email delivery status changes.

**Acceptance Criteria:**

1. POST `/api/v1/webhooks` accepts `url`, `events` (array of event types to subscribe to), and optional `secret` (for HMAC signature verification).
2. Supported events: `email.sent`, `email.delivered`, `email.bounced`, `email.complained`, `email.opened`, `email.clicked`, `email.failed`.
3. Webhook URL must be HTTPS.
4. SSRF protection: webhook URLs are validated against an allowlist of public IP ranges; private/loopback/link-local addresses are rejected.
5. A test ping is sent to verify the URL is reachable.
6. Maximum 10 webhook endpoints per tenant.

#### US-8.2: Receive Webhook Notifications

**As a** developer,
**I want** my webhook endpoints to receive real-time notifications for email events,
**so that** I can update my application state accordingly.

**Acceptance Criteria:**

1. Webhooks are delivered as POST requests with JSON body containing: `event`, `message_id`, `timestamp`, `data` (event-specific payload).
2. Each webhook includes an `X-SendNex-Signature` header (HMAC-SHA256 of the body using the endpoint's secret).
3. Webhook delivery is retried up to 5 times with exponential backoff (1s, 10s, 60s, 300s, 3600s) on non-2xx responses.
4. Failed webhooks after all retries are logged and surfaced on the dashboard.
5. Webhook delivery latency target: < 5 seconds from event occurrence.

#### US-8.3: Manage Webhook Endpoints

**As a** developer,
**I want** to list, update, and delete webhook endpoints,
**so that** I can maintain my integration configuration.

**Acceptance Criteria:**

1. GET `/api/v1/webhooks` lists all endpoints with URL, events, status, and delivery statistics.
2. PUT `/api/v1/webhooks/{id}` updates URL, events, or secret.
3. DELETE `/api/v1/webhooks/{id}` removes the endpoint.
4. GET `/api/v1/webhooks/{id}/deliveries` shows webhook delivery logs (last 100 deliveries per endpoint with status and response code).

---

### Epic 9: Inbound Email

#### US-9.1: Receive Inbound Emails

**As a** developer,
**I want** SendNex to receive inbound emails addressed to my domain,
**so that** I can process replies and automated responses within my application.

**Acceptance Criteria:**

1. Inbound emails are received via AWS SES inbound rule sets and stored in the platform.
2. GET `/api/v1/inbound/emails` returns a paginated list of received inbound emails per tenant.
3. GET `/api/v1/inbound/emails/{id}` returns the full inbound email including headers, body, and attachments.
4. DELETE `/api/v1/inbound/emails/{id}` deletes the stored inbound email.
5. POST `/api/v1/inbound/emails/{id}/retry-webhook` retries the webhook dispatch for a specific inbound email.

#### US-9.2: Configure Inbound Routing Rules

**As a** developer,
**I want** to define routing rules that determine what happens when an inbound email arrives,
**so that** I can automate processing in my application.

**Acceptance Criteria:**

1. POST `/api/v1/inbound/rules` creates a new routing rule with: `name`, `match_criteria` (recipient address pattern), `action` (webhook / forward / store), and action parameters (webhook URL, forward-to address).
2. GET `/api/v1/inbound/rules` lists all inbound rules for the tenant.
3. GET `/api/v1/inbound/rules/{id}` returns rule detail.
4. PUT `/api/v1/inbound/rules/{id}` updates rule criteria or action.
5. DELETE `/api/v1/inbound/rules/{id}` removes the rule.
6. Webhook action URLs are subject to SSRF protection.
7. Rules are evaluated in priority order; first match wins.

---

### Epic 10: Multi-Tenant Authentication

#### US-10.1: Tenant Registration

**As a** new tenant,
**I want** to register an account on SendNex,
**so that** I can start sending emails under my own isolated workspace.

**Acceptance Criteria:**

1. POST `/api/v1/auth/register` accepts `email`, `password`, `company_name`.
2. Passwords are bcrypt-hashed before storage.
3. Tenant is provisioned with a Free plan subscription.
4. JWT access token is returned on successful registration.
5. Duplicate email registration returns 409 Conflict.

#### US-10.2: Tenant Login

**As a** tenant,
**I want** to log in and receive a session token,
**so that** I can authenticate API calls and access the dashboard.

**Acceptance Criteria:**

1. POST `/api/v1/auth/login` accepts `email` and `password`.
2. Returns a JWT access token on success.
3. Invalid credentials return 401 Unauthorized with a generic error (no field-level leakage).
4. Login attempts are rate-limited (max 10 per minute per IP).

---

### Epic 11: Admin Panel

#### US-11.1: Admin Authentication

**As a** platform admin,
**I want** to log in to the admin panel with a separate admin credential,
**so that** I have isolated privileged access to platform governance tools.

**Acceptance Criteria:**

1. POST `/api/admin/auth/login` accepts admin credentials.
2. On success, issues an HMAC-signed session cookie (not a JWT shared with tenant auth).
3. Admin session cookies are HttpOnly, Secure, SameSite=Strict.
4. All admin endpoints require a valid admin session cookie; tenant JWT is not accepted.
5. Failed admin login is logged to the audit trail.

#### US-11.2: Tenant Management

**As a** platform admin,
**I want** to view, manage, and create tenant accounts,
**so that** I can govern the platform's tenant base.

**Acceptance Criteria:**

1. GET `/api/admin/tenants` returns a paginated list of all tenants with: name, email, plan, created_at, email counts, status.
2. GET `/api/admin/tenants/{id}` returns full tenant detail including usage metrics.
3. POST `/api/admin/tenants` creates a new tenant directly (bypass public registration).
4. Admin can suspend or reactivate a tenant.

#### US-11.3: Platform Analytics

**As a** platform admin,
**I want** to view platform-wide email analytics,
**so that** I can monitor overall platform health and usage trends.

**Acceptance Criteria:**

1. GET `/api/admin/analytics/summary` returns platform-level totals: total emails sent, delivery rate, bounce rate, active tenants, total tenants.
2. GET `/api/admin/analytics/tenant-rankings` returns tenants ranked by email volume.
3. GET `/api/admin/analytics/timeline` returns platform-wide time-series data.
4. All admin analytics are cross-tenant (not scoped to a single tenant).

#### US-11.4: Audit Logging

**As a** platform admin,
**I want** to view a chronological audit log of all admin and significant tenant actions,
**so that** I can investigate issues and maintain compliance.

**Acceptance Criteria:**

1. GET `/api/admin/audit-logs` returns a paginated list of audit entries.
2. Entries include: `actor` (admin or tenant ID), `action`, `target`, `timestamp`, `ip_address`, `details`.
3. Audited actions include: admin login, tenant create/suspend/activate, subscription change, suppression list manual change, API key revocation.
4. Audit log entries are immutable (no delete or update).

#### US-11.5: System Health (Admin)

**As a** platform admin,
**I want** to view the system health status from the admin panel,
**so that** I can monitor infrastructure component status.

**Acceptance Criteria:**

1. GET `/api/admin/health` returns health status for all platform components: API, worker, database, Redis, RabbitMQ, SES.
2. Response includes latency measurements for each component.
3. Queue depth and worker throughput are included.

---

### Epic 12: Billing

#### US-12.1: View Available Plans

**As a** tenant,
**I want** to view available subscription plans and their features,
**so that** I can choose the right plan for my usage.

**Acceptance Criteria:**

1. GET `/api/v1/billing/plans` returns all available plans: Free, Starter, Pro, Business, Enterprise.
2. Each plan includes: `name`, `price_monthly`, `email_quota`, `features`, `overage_rate`.
3. Current tenant's active plan is indicated.

#### US-12.2: Subscribe to a Plan

**As a** tenant,
**I want** to subscribe to a paid plan,
**so that** I can unlock higher email quotas and features.

**Acceptance Criteria:**

1. POST `/api/v1/billing/subscribe` accepts `plan_id` and initiates a Paystack subscription.
2. Paystack checkout flow is returned for the tenant to complete payment.
3. On payment confirmation via Paystack webhook, tenant's plan and quota are updated immediately.
4. Failed payments are logged and the tenant retains their current plan.

#### US-12.3: View Current Subscription

**As a** tenant,
**I want** to view my current subscription details,
**so that** I can see my quota, renewal date, and plan status.

**Acceptance Criteria:**

1. GET `/api/v1/billing/subscription` returns: `plan`, `status`, `quota_used`, `quota_limit`, `renewal_date`, `paystack_subscription_id`.
2. Quota usage is calculated from the current billing period.

#### US-12.4: View Invoice History

**As a** tenant,
**I want** to view and download my billing invoices,
**so that** I can maintain financial records.

**Acceptance Criteria:**

1. GET `/api/v1/billing/invoices` returns a paginated list of all invoices with: `invoice_id`, `amount`, `status`, `created_at`, `pdf_url`.
2. Invoices are linked to Paystack transaction references.
3. PDF invoices are downloadable.

#### US-12.5: Cancel Subscription

**As a** tenant,
**I want** to cancel my subscription,
**so that** I can stop recurring charges if I no longer need the service.

**Acceptance Criteria:**

1. DELETE `/api/v1/billing/subscription` cancels the active Paystack subscription.
2. Cancellation takes effect at the end of the current billing period (no prorated refunds).
3. Tenant is downgraded to Free plan at the end of the period.
4. Confirmation email is sent to the tenant's registered address at `noreply@sendnex.xyz`.

---

## 12. Feature Prioritization

### P0 - MVP Launch (Delivered — Sprint 1-2)

Core infrastructure and basic sending capability.

| Feature | User Story | Status |
|---------|-----------|--------|
| Single email send | US-1.1 | Done |
| Send with template | US-1.3 | Done |
| Send with attachments | US-1.4 | Done |
| Create/update/list templates | US-2.1, US-2.2, US-2.3 | Done |
| Add/verify domain | US-3.1, US-3.2, US-3.3 | Done |
| API key create/revoke | US-5.1, US-5.3 | Done |
| Automatic bounce suppression | US-6.1 | Done |
| Automatic complaint suppression | US-6.2 | Done |
| Basic send logs | US-4.1 | Done |
| Dashboard overview | US-7.1 | Done |
| Email log viewer | US-7.2 | Done |
| Tenant auth (register, login) | US-10.1, US-10.2 | Done |

### P1 - Enhanced Platform (Delivered — Sprint 3-4)

Full-featured transactional platform.

| Feature | User Story | Status |
|---------|-----------|--------|
| Batch email send (with quota enforcement) | US-1.2 | Done |
| CC/BCC support | US-1.5 | Done |
| Template preview | US-2.5 | Done |
| Template delete/restore | US-2.4 | Done |
| Template versioning + rollback | US-2.6, US-2.7 | Done |
| Outbound analytics (summary + timeline) | US-4.2 | Done |
| Inbound analytics summary | US-4.3 | Done |
| Open tracking | US-4.4 | Done |
| Click tracking | US-4.5 | Done |
| Email event history | US-1.7 | Done |
| API key rotation | US-5.4 | Done |
| API key list | US-5.2 | Done |
| Suppression list management | US-6.3 | Done |
| Analytics dashboard | US-7.5 | Done |
| Template manager UI | US-7.3 | Done |
| Domain manager UI | US-7.4 | Done |
| Suppression list UI | US-7.6 | Done |
| Webhook notifications (with deliveries) | US-8.1, US-8.2, US-8.3 | Done |
| Remove domain | US-3.4 | Done |
| SSRF protection on webhook URLs | US-8.1 AC4 | Done |

### P2 - SaaS Platform (Delivered — Sprint 5)

Multi-tenancy, billing, inbound, admin, and scheduled sends.

| Feature | User Story | Status |
|---------|-----------|--------|
| Scheduled email delivery | US-1.6 | Done |
| Inbound email receiving | US-9.1 | Done |
| Inbound routing rules | US-9.2 | Done |
| Subscription billing (Paystack) | US-12.1 – US-12.5 | Done |
| Billing dashboard UI | US-7.7 | Done |
| Admin authentication | US-11.1 | Done |
| Admin tenant management | US-11.2 | Done |
| Admin platform analytics | US-11.3 | Done |
| Admin audit logging | US-11.4 | Done |
| Admin health check | US-11.5 | Done |
| Content review gate (flagged emails) | — | Done |

### P3 - Future Roadmap

| Feature | Rationale |
|---------|-----------|
| Public API documentation portal (OpenAPI/Swagger) | Developer onboarding |
| SDKs (.NET, Node.js, Python) | Developer experience |
| IP warmup automation | Deliverability at scale |
| Email content validation (spam score) | Deliverability improvement |
| SMS/push notification channel | Multi-channel expansion |
| Dedicated IP support | High-volume tenants |

---

## 13. Non-Functional Requirements

### Performance

| Metric | Target | Notes |
|--------|--------|-------|
| API response time (single send, p50) | < 100ms | Enqueue only, not delivery |
| API response time (single send, p95) | < 200ms | |
| API response time (batch send, p95) | < 500ms | Up to 100 emails |
| Template rendering time (p95) | < 50ms | Liquid template engine |
| Email delivery time (enqueue to SES accept) | < 5 seconds | Worker processing |
| Click tracking redirect time | < 100ms | No perceptible delay |
| Dashboard page load time | < 2 seconds | Including data fetch |
| Throughput | 100 emails/second sustained | Worker can scale horizontally if needed |
| Queue depth tolerance | 50,000 messages | Before backpressure alert |
| Scheduled send check interval | 30 seconds | Acceptable delivery jitter |

### Security

| Requirement | Implementation |
|-------------|----------------|
| Tenant API authentication | API key in `Authorization: Bearer` header |
| Admin authentication | HMAC-signed session cookies (separate from tenant JWT) |
| API key storage | SHA-256 hashed, never stored in plaintext |
| Transport security | HTTPS only (TLS 1.2+), HSTS header, certbot via nginx |
| Rate limiting | 100 requests/second per API key (configurable) |
| Input validation | All inputs sanitized; parameterized SQL queries |
| Dashboard authentication | Username/password with bcrypt hashing (multi-tenant) |
| Dashboard sessions | Secure, HttpOnly, SameSite=Strict cookies |
| Webhook signatures | HMAC-SHA256 (`X-SendNex-Signature`) for webhook payload verification |
| SSRF protection | Webhook and inbound rule URLs validated against public IP ranges only |
| Secret management | AWS SES credentials and Paystack keys in environment variables, not in code |
| Dependency security | Automated vulnerability scanning (Dependabot or similar) |
| CORS | Disabled for API (server-to-server); enabled for dashboard origin only |

### Reliability

| Requirement | Target |
|-------------|--------|
| System uptime | > 99.5% monthly |
| Message durability | Zero message loss (RabbitMQ persistent queues with disk-backed storage) |
| Dead letter handling | Failed messages routed to DLQ after 3 retries |
| Database backups | Daily automated backup to S3, 30-day retention |
| Recovery Time Objective (RTO) | < 1 hour (docker-compose restart) |
| Recovery Point Objective (RPO) | < 24 hours (daily backup) |
| Graceful degradation | If SES is unavailable, messages stay in queue until recovery |

### Scalability

| Metric | Current Target | Growth Path |
|--------|---------------|-------------|
| Emails per day | 10,000 | Scale to 100K with same infrastructure |
| Emails per month | 300,000 | Scale to 3M with horizontal worker scaling |
| Active tenants | 50 | No practical limit with current schema |
| API keys per tenant | 20 | No practical limit |
| Templates per tenant | 200 | No practical limit |
| Domains per tenant | 20 | SES limit: 10,000 |
| Log retention | 90 days | Configurable, archive to S3 for long-term |
| Concurrent API connections | 500 | .NET handles 10K+ concurrent with Kestrel |

### Observability

| Requirement | Implementation |
|-------------|----------------|
| Structured logging | Serilog with JSON output |
| Log aggregation | Console output captured by Docker |
| Health check endpoint | GET `/health` returns API, DB, Redis, RabbitMQ, SES status |
| Admin health endpoint | GET `/api/admin/health` returns extended diagnostics |
| Metrics | Custom metrics exposed via dashboard (send rate, error rate, queue depth) |
| Alerting | Dashboard alerts for: high bounce rate, complaint threshold, queue backup, worker down |
| External monitoring | UptimeRobot or similar pinging `/health` every 60 seconds |

---

## 14. API Contract

### Authentication

**Tenant API (all tenant endpoints):**
```
Authorization: Bearer snx_live_aBcDeFgH1234567890...
```

**Admin API (all `/api/admin/*` endpoints):**
```
Cookie: admin_session=<HMAC-signed session token>
```

### Base URLs

```
Tenant API:  https://sendnex.xyz/api/v1
Admin API:   https://sendnex.xyz/api/admin
Health:      https://sendnex.xyz/health
```

### Common Response Format

**Success:**
```json
{
  "success": true,
  "data": { ... }
}
```

**Error:**
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Human-readable error description",
    "details": [
      { "field": "to", "message": "Invalid email address format" }
    ]
  }
}
```

### Key Endpoints

---

#### POST /api/v1/emails/send

Send a single transactional email.

**Request:**
```json
{
  "from": {
    "email": "invoices@cashtrack.app",
    "name": "CashTrack"
  },
  "to": [
    {
      "email": "client@example.com",
      "name": "John Doe"
    }
  ],
  "cc": [],
  "bcc": [],
  "subject": "Invoice #INV-2026-001",
  "html_body": "<h1>Your Invoice</h1><p>Amount: $500.00</p>",
  "text_body": "Your Invoice\nAmount: $500.00",
  "template_id": null,
  "variables": null,
  "attachments": [
    {
      "filename": "invoice-2026-001.pdf",
      "content": "base64encodedcontent...",
      "content_type": "application/pdf"
    }
  ],
  "scheduled_at": null,
  "tags": ["invoice", "cashtrack"],
  "track_opens": true,
  "track_clicks": true,
  "metadata": {
    "invoice_id": "INV-2026-001",
    "app": "cashtrack"
  }
}
```

**Response (202 Accepted):**
```json
{
  "success": true,
  "data": {
    "message_id": "msg_a1b2c3d4e5f6",
    "status": "queued",
    "queued_at": "2026-04-12T10:30:00Z"
  }
}
```

---

#### POST /api/v1/emails/send (scheduled)

**Request (scheduled for future delivery):**
```json
{
  "from": { "email": "noreply@sendnex.xyz", "name": "SendNex" },
  "to": [{ "email": "user@example.com" }],
  "subject": "Your weekly report",
  "html_body": "<p>Here is your weekly summary.</p>",
  "scheduled_at": "2026-04-13T08:00:00Z"
}
```

**Response (202 Accepted):**
```json
{
  "success": true,
  "data": {
    "message_id": "msg_sched_x9y8z7",
    "status": "scheduled",
    "scheduled_at": "2026-04-13T08:00:00Z"
  }
}
```

---

#### POST /api/v1/emails/batch

Send up to 100 emails in a single request.

**Request:**
```json
{
  "emails": [
    {
      "from": { "email": "invoices@cashtrack.app", "name": "CashTrack" },
      "to": [{ "email": "client1@example.com", "name": "Alice" }],
      "template_id": "tmpl_payment_reminder",
      "variables": { "client_name": "Alice", "amount": "$200.00" }
    },
    {
      "from": { "email": "invoices@cashtrack.app", "name": "CashTrack" },
      "to": [{ "email": "client2@example.com", "name": "Bob" }],
      "template_id": "tmpl_payment_reminder",
      "variables": { "client_name": "Bob", "amount": "$350.00" }
    }
  ]
}
```

**Response (202 Accepted):**
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

---

#### GET /api/v1/emails/{message_id}/events

Get delivery event history for a specific email.

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "message_id": "msg_a1b2c3d4e5f6",
    "events": [
      { "event_type": "queued", "timestamp": "2026-04-12T10:30:00Z", "metadata": {} },
      { "event_type": "sent", "timestamp": "2026-04-12T10:30:03Z", "metadata": { "ses_message_id": "ses_xyz" } },
      { "event_type": "delivered", "timestamp": "2026-04-12T10:30:05Z", "metadata": {} },
      { "event_type": "opened", "timestamp": "2026-04-12T11:15:22Z", "metadata": { "country": "NG" } }
    ]
  }
}
```

---

#### POST /api/v1/templates

**Request:**
```json
{
  "name": "payment_reminder",
  "subject_template": "Payment Reminder: {{ invoice_number }}",
  "html_body": "<!DOCTYPE html><html><body><h1>Hi {{ client_name }},</h1><p>Invoice <strong>{{ invoice_number }}</strong> for <strong>{{ amount }}</strong> is due on {{ due_date }}.</p><a href=\"{{ payment_link }}\">Pay Now</a></body></html>",
  "text_body": "Hi {{ client_name }},\n\nInvoice {{ invoice_number }} for {{ amount }} is due on {{ due_date }}.\n\nPay here: {{ payment_link }}",
  "variables_schema": {
    "type": "object",
    "required": ["client_name", "invoice_number", "amount", "due_date", "payment_link"],
    "properties": {
      "client_name": { "type": "string" },
      "invoice_number": { "type": "string" },
      "amount": { "type": "string" },
      "due_date": { "type": "string" },
      "payment_link": { "type": "string", "format": "uri" }
    }
  }
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "template_id": "tmpl_a1b2c3d4",
    "name": "payment_reminder",
    "version": 1,
    "created_at": "2026-04-12T10:00:00Z"
  }
}
```

---

#### POST /api/v1/templates/{id}/rollback

**Request:**
```json
{
  "version": 2
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "template_id": "tmpl_a1b2c3d4",
    "name": "payment_reminder",
    "version": 5,
    "rolled_back_to_version": 2,
    "updated_at": "2026-04-12T11:00:00Z"
  }
}
```

---

#### POST /api/v1/domains

**Request:**
```json
{
  "domain": "notifications.cashtrack.app"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "domain_id": "dom_x1y2z3",
    "domain": "notifications.cashtrack.app",
    "status": "pending_verification",
    "dns_records": [
      {
        "type": "TXT",
        "name": "notifications.cashtrack.app",
        "value": "v=spf1 include:amazonses.com ~all",
        "purpose": "SPF"
      },
      {
        "type": "CNAME",
        "name": "abcdef._domainkey.notifications.cashtrack.app",
        "value": "abcdef.dkim.amazonses.com",
        "purpose": "DKIM"
      },
      {
        "type": "CNAME",
        "name": "ghijkl._domainkey.notifications.cashtrack.app",
        "value": "ghijkl.dkim.amazonses.com",
        "purpose": "DKIM"
      },
      {
        "type": "CNAME",
        "name": "mnopqr._domainkey.notifications.cashtrack.app",
        "value": "mnopqr.dkim.amazonses.com",
        "purpose": "DKIM"
      },
      {
        "type": "TXT",
        "name": "_dmarc.notifications.cashtrack.app",
        "value": "v=DMARC1; p=quarantine; rua=mailto:dmarc@sendnex.xyz",
        "purpose": "DMARC"
      }
    ],
    "created_at": "2026-04-12T10:00:00Z"
  }
}
```

---

#### POST /api/v1/api-keys

**Request:**
```json
{
  "name": "CashTrack Production",
  "allowed_domains": ["notifications.cashtrack.app"]
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "key_id": "key_m1n2o3p4",
    "name": "CashTrack Production",
    "api_key": "snx_live_aBcDeFgH1234567890xYzWvUtSrQpOnMlKjIhG",
    "prefix": "snx_live",
    "allowed_domains": ["notifications.cashtrack.app"],
    "created_at": "2026-04-12T10:00:00Z"
  }
}
```

> **Warning:** The `api_key` field is returned only on creation. Store it securely. It cannot be retrieved again.

---

#### POST /api/v1/inbound/rules

**Request:**
```json
{
  "name": "Support reply handler",
  "match_criteria": {
    "recipient_pattern": "support@*"
  },
  "action": "webhook",
  "action_params": {
    "url": "https://eventra.app/webhooks/inbound-email",
    "secret": "wh_secret_abc123"
  },
  "priority": 1
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "rule_id": "rule_r1s2t3",
    "name": "Support reply handler",
    "action": "webhook",
    "priority": 1,
    "created_at": "2026-04-12T10:00:00Z"
  }
}
```

---

#### GET /api/v1/analytics/outbound/summary

**Request:**
```
GET /api/v1/analytics/outbound/summary?start_date=2026-04-01&end_date=2026-04-12
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "total_sent": 4250,
    "total_delivered": 4180,
    "total_bounced": 45,
    "total_complained": 3,
    "total_opened": 2890,
    "total_clicked": 1205,
    "delivery_rate": 98.35,
    "bounce_rate": 1.06,
    "complaint_rate": 0.07,
    "open_rate": 69.14,
    "click_rate": 28.83
  }
}
```

---

#### GET /api/v1/billing/plans

**Response (200 OK):**
```json
{
  "success": true,
  "data": [
    { "plan_id": "free", "name": "Free", "price_monthly": 0, "email_quota": 1000, "features": ["Basic analytics", "1 domain"] },
    { "plan_id": "starter", "name": "Starter", "price_monthly": 9, "email_quota": 10000, "features": ["Analytics", "5 domains", "Webhooks"] },
    { "plan_id": "pro", "name": "Pro", "price_monthly": 29, "email_quota": 50000, "features": ["Advanced analytics", "20 domains", "Webhooks", "Inbound email", "Scheduled sends"] },
    { "plan_id": "business", "name": "Business", "price_monthly": 79, "email_quota": 200000, "features": ["All Pro features", "Unlimited domains", "Priority support"] },
    { "plan_id": "enterprise", "name": "Enterprise", "price_monthly": 0, "email_quota": -1, "features": ["Custom quota", "Dedicated support", "SLA"] }
  ]
}
```

---

#### GET /api/admin/analytics/summary

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "total_tenants": 12,
    "active_tenants_30d": 8,
    "total_emails_sent_30d": 87500,
    "platform_delivery_rate": 98.6,
    "platform_bounce_rate": 0.95,
    "platform_complaint_rate": 0.04
  }
}
```

---

#### GET /health

**Response (200 OK):**
```json
{
  "status": "healthy",
  "version": "2.0.0",
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

### Full Endpoint Summary (39 Tenant + 17 Admin = 56 Total)

#### Tenant Endpoints (39)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/auth/register` | Register tenant |
| POST | `/api/v1/auth/login` | Tenant login |
| POST | `/api/v1/emails/send` | Send single email |
| POST | `/api/v1/emails/batch` | Send batch emails (quota enforced) |
| GET | `/api/v1/emails` | List emails |
| GET | `/api/v1/emails/{message_id}` | Get email detail |
| GET | `/api/v1/emails/{message_id}/events` | Get email event history |
| GET | `/api/v1/templates` | List templates |
| GET | `/api/v1/templates/{id}` | Get template |
| POST | `/api/v1/templates` | Create template |
| PUT | `/api/v1/templates/{id}` | Update template |
| DELETE | `/api/v1/templates/{id}` | Delete template |
| PATCH | `/api/v1/templates/{id}/restore` | Restore template |
| POST | `/api/v1/templates/{id}/preview` | Preview template |
| GET | `/api/v1/templates/{id}/versions` | List template versions |
| POST | `/api/v1/templates/{id}/rollback` | Rollback template version |
| GET | `/api/v1/domains` | List domains |
| GET | `/api/v1/domains/{id}` | Get domain |
| POST | `/api/v1/domains` | Add domain |
| POST | `/api/v1/domains/{id}/verify` | Verify domain |
| DELETE | `/api/v1/domains/{id}` | Remove domain |
| GET | `/api/v1/api-keys` | List API keys |
| POST | `/api/v1/api-keys` | Create API key |
| DELETE | `/api/v1/api-keys/{id}` | Revoke API key |
| POST | `/api/v1/api-keys/{id}/rotate` | Rotate API key |
| GET | `/api/v1/webhooks` | List webhooks |
| POST | `/api/v1/webhooks` | Create webhook |
| PUT | `/api/v1/webhooks/{id}` | Update webhook |
| DELETE | `/api/v1/webhooks/{id}` | Delete webhook |
| GET | `/api/v1/webhooks/{id}/deliveries` | Get webhook delivery history |
| GET | `/api/v1/analytics/outbound/summary` | Outbound analytics summary |
| GET | `/api/v1/analytics/outbound/timeline` | Outbound analytics timeline |
| GET | `/api/v1/analytics/inbound/summary` | Inbound analytics summary |
| GET | `/api/v1/inbound/emails` | List inbound emails |
| GET | `/api/v1/inbound/emails/{id}` | Get inbound email |
| DELETE | `/api/v1/inbound/emails/{id}` | Delete inbound email |
| POST | `/api/v1/inbound/emails/{id}/retry-webhook` | Retry inbound email webhook |
| GET | `/api/v1/inbound/rules` | List inbound rules |
| GET | `/api/v1/inbound/rules/{id}` | Get inbound rule |
| POST | `/api/v1/inbound/rules` | Create inbound rule |
| PUT | `/api/v1/inbound/rules/{id}` | Update inbound rule |
| DELETE | `/api/v1/inbound/rules/{id}` | Delete inbound rule |
| GET | `/api/v1/suppressions` | List suppressions |
| POST | `/api/v1/suppressions` | Add suppression |
| DELETE | `/api/v1/suppressions/{email}` | Remove suppression |
| GET | `/api/v1/billing/plans` | List billing plans |
| POST | `/api/v1/billing/subscribe` | Subscribe to plan |
| GET | `/api/v1/billing/subscription` | Get current subscription |
| GET | `/api/v1/billing/invoices` | List invoices |
| DELETE | `/api/v1/billing/subscription` | Cancel subscription |

> Note: The 39 tenant endpoint count reflects the current implemented API surface. Template versions/rollback, inbound retry-webhook, and webhook deliveries endpoint may be counted within their parent resource groups depending on routing configuration.

#### Admin Endpoints (17)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/admin/auth/login` | Admin login |
| GET | `/api/admin/tenants` | List all tenants |
| GET | `/api/admin/tenants/{id}` | Get tenant detail |
| POST | `/api/admin/tenants` | Create tenant |
| GET | `/api/admin/analytics/summary` | Platform analytics summary |
| GET | `/api/admin/analytics/tenant-rankings` | Tenant rankings by volume |
| GET | `/api/admin/analytics/timeline` | Platform analytics timeline |
| GET | `/api/admin/health` | System health (extended) |
| GET | `/api/admin/audit-logs` | List audit logs |

#### Shared Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Public system health check |

---

## 15. Product Phases / Roadmap

### Phase 1: MVP (Sprints 1-2) — COMPLETE

**Goal:** Replace all SaaS email providers. All applications send through SendNex.

| Deliverable | Status |
|------------|--------|
| Project setup: solution structure, Docker Compose, PostgreSQL schema, Redis, RabbitMQ | Done |
| Core API: email send, template CRUD, domain registration/verification, API key management | Done |
| Worker service: queue consumer, Liquid template rendering, SES integration, bounce/complaint handling | Done |
| Dashboard MVP: overview page, log viewer | Done |
| CashTrack migrated to SendNex | Done |

### Phase 2: Enhanced Platform (Sprints 3-4) — COMPLETE

**Goal:** Full feature set for a production-grade transactional email platform.

| Deliverable | Status |
|------------|--------|
| Batch sending with quota enforcement | Done |
| CC/BCC, template preview, template versioning + rollback | Done |
| Open tracking, click tracking | Done |
| Analytics API and dashboard with charts | Done |
| API key rotation, suppression list management (API + dashboard) | Done |
| Domain manager UI, template manager UI | Done |
| Webhook notifications with delivery tracking | Done |
| SSRF protection on webhook URLs | Done |
| Email event history endpoint | Done |

### Phase 3: Multi-Tenant SaaS (Sprint 5) — COMPLETE

**Goal:** Open the platform to external paying tenants. Launch Eventra and CashTrack as customers.

| Deliverable | Status |
|------------|--------|
| Multi-tenant account system with isolated data | Done |
| Tenant registration and authentication (JWT) | Done |
| Subscription billing via Paystack (Free / Starter / Pro / Business / Enterprise) | Done |
| Invoice management | Done |
| Admin panel with HMAC-signed session cookie auth | Done |
| Admin: tenant management, platform analytics, audit logging, system health | Done |
| Inbound email receiving and routing rules | Done |
| Scheduled email delivery | Done |
| Content review gate for flagged emails | Done |
| Eventra onboarded | Done |
| CashTrack migrated to paid tenant | Done |

### Phase 4: Future Roadmap (Post Sprint 5)

| Milestone | Deliverable |
|-----------|------------|
| 4.1 | Public API documentation portal (OpenAPI/Swagger) |
| 4.2 | Client SDKs (.NET, Node.js, Python) |
| 4.3 | IP warmup automation |
| 4.4 | Email content spam-score validation |
| 4.5 | Marketing site at sendnex.xyz |
| 4.6 | SMS / push notification channel (multi-channel expansion) |

---

## 16. Constraints & Assumptions

### Constraints

| # | Constraint | Impact |
|---|-----------|--------|
| 1 | **Single VPS deployment.** Hetzner CX22 (2 vCPU, 4GB RAM, 40GB SSD). | No horizontal scaling without infrastructure changes. Worker service is the bottleneck for throughput. |
| 2 | **AWS SES sending limits.** Default: 14 emails/second in production. | Must implement send throttling in the worker to stay within limits. Can request increases. |
| 3 | **Single developer.** Israel is the sole developer and operator. | Development velocity limited. Prioritize ruthlessly. Automate operations where possible. |
| 4 | **Budget ceiling.** Total infrastructure cost must stay under $20/month (operator side). | Rules out dedicated IPs ($24.95/month), multi-server setups, and premium monitoring tools. |
| 5 | **.NET 10 ecosystem.** Technology stack is fixed. | Template engine must be .NET-compatible (Fluid for Liquid). |
| 6 | **Paystack payment gateway.** Billing is Nigeria/Africa-first. | Limits tenant payment method options to Paystack-supported cards and banks. |

### Assumptions

| # | Assumption | Risk if Wrong |
|---|-----------|---------------|
| 1 | Monthly send volume will remain under 300K emails platform-wide for the first 6 months. | VPS may need upgrade; SES costs may exceed budget. |
| 2 | AWS SES production access is active and approved. | New sending domains cannot be used until approved. |
| 3 | Eventra and CashTrack will remain active tenants on paid plans post-Sprint 5. | MRR targets will not be met. |
| 4 | Hetzner VPS provides sufficient uptime (99.9% SLA). | Need fallback hosting plan. Mitigation: daily backups enable quick migration. |
| 5 | Israel's sender reputation is clean (no prior blacklisting). | Deliverability issues from day one. Mitigation: check blacklists before launch. |
| 6 | Liquid template syntax (via Fluid library) is sufficient for all tenant template needs. | May need to switch to Handlebars.NET or custom engine. |
| 7 | RabbitMQ on a single node is reliable enough for current scale. | Message loss risk. Mitigation: persistent queues with disk storage. |
| 8 | Browser-based Next.js 16 dashboard is acceptable (no mobile requirement for tenants). | If mobile monitoring is needed, would require responsive design work. |
| 9 | Paystack webhooks for billing events are reliable enough for subscription lifecycle management. | Subscription state drift if Paystack webhook delivery fails. Mitigation: reconciliation job. |

---

## 17. Glossary

| Term | Definition |
|------|-----------|
| **Transactional email** | An email triggered by a user action or system event (e.g., invoice, password reset, notification). Not a marketing email. |
| **AWS SES** | Amazon Simple Email Service. Managed SMTP delivery service. Charges $0.10 per 1,000 emails. |
| **SPF** | Sender Policy Framework. DNS TXT record that specifies which mail servers are authorized to send email on behalf of a domain. |
| **DKIM** | DomainKeys Identified Mail. Cryptographic signature in email headers that verifies the sender's domain identity. |
| **DMARC** | Domain-based Message Authentication, Reporting, and Conformance. DNS policy that tells receiving mail servers how to handle emails that fail SPF/DKIM checks. |
| **Hard bounce** | A permanent delivery failure (e.g., invalid email address, non-existent domain). The address should never be retried. |
| **Soft bounce** | A temporary delivery failure (e.g., mailbox full, server temporarily unavailable). Can be retried. |
| **Suppression list** | A list of email addresses that are blocked from receiving emails, typically due to hard bounces or spam complaints. |
| **Complaint** | When a recipient marks an email as spam. ISPs report this back to the sender via feedback loops. |
| **Sender reputation** | A score assigned by ISPs based on sending behavior (bounce rate, complaint rate, volume patterns). Affects deliverability. |
| **SNS** | Amazon Simple Notification Service. Used by SES to send bounce, complaint, and delivery notifications to the application. |
| **Dead letter queue (DLQ)** | A queue where messages are routed after failing processing multiple times. Prevents infinite retry loops. |
| **Liquid** | A template language created by Shopify. Uses `{{ variable }}` syntax for dynamic content. The Fluid library provides .NET implementation. |
| **Webhook** | An HTTP callback that sends real-time notifications to a specified URL when an event occurs. |
| **HMAC-SHA256** | Hash-based Message Authentication Code using SHA-256. Used to verify webhook payload authenticity and admin session cookies. |
| **Rate limiting** | Restricting the number of API requests a client can make within a time window. |
| **API key** | A secret token used to authenticate API requests. Identifies which tenant application is making the request. |
| **Tenant** | An isolated organization or team using SendNex as a service. Data, API keys, templates, and analytics are fully isolated per tenant. |
| **Inbound email** | An email received by SendNex addressed to a tenant's registered domain, routed according to inbound rules. |
| **Inbound routing rule** | A tenant-configured rule that defines how to process inbound emails matching a recipient pattern (webhook, forward, or store). |
| **Scheduled send** | An email queued with a future `scheduled_at` timestamp. Delivered by the worker at the specified time. |
| **Quota** | The maximum number of emails a tenant can send within a billing period, determined by their subscription plan. |
| **Paystack** | A Nigerian payment infrastructure provider used for subscription billing and invoicing. |
| **SSRF** | Server-Side Request Forgery. An attack where an application is manipulated into making requests to internal network resources. SendNex blocks private/loopback IP ranges on webhook URLs to prevent this. |
| **Hetzner** | A German hosting provider offering affordable cloud VPS instances. SendNex is hosted on a CX22 instance. |
| **RabbitMQ** | An open-source message broker that implements AMQP. Used for reliable async message processing. |
| **Minimal API** | A .NET 10 approach for building HTTP APIs with minimal boilerplate code. |
| **Next.js 16** | A React framework for building full-stack web applications with server-side rendering and static generation. Used for both the tenant dashboard and admin panel. |
| **Docker Compose** | A tool for defining and running multi-container Docker applications using a YAML configuration file. |
| **HSTS** | HTTP Strict Transport Security. Forces browsers to use HTTPS. |
| **WAL** | Write-Ahead Logging. PostgreSQL's mechanism for ensuring data integrity and enabling point-in-time recovery. |
| **certbot** | An ACME client for automatically obtaining and renewing TLS certificates from Let's Encrypt. Used with nginx on the SendNex VPS. |
| **nginx** | A high-performance HTTP server and reverse proxy. Used to terminate TLS and route traffic to the .NET API on the SendNex VPS. |
| **MRR** | Monthly Recurring Revenue. The sum of all active paid tenant subscription fees in a given month. |

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-27 | Senior Business Analyst | Initial draft - complete BRD/PRD for personal-use self-hosted email platform |
| 2.0 | 2026-04-12 | Senior Business Analyst | Product renamed to SendNex. Updated to reflect multi-tenant SaaS architecture, external tenants (Eventra, CashTrack), Paystack subscription billing, inbound email routing, scheduled sends, template versioning with rollback, admin panel with HMAC auth, platform analytics, audit logging, SSRF protection, batch quota enforcement, email events endpoint. Technology updated to .NET 10 and Next.js 16. Domain updated to sendnex.xyz. Full API endpoint table updated (39 tenant + 17 admin). All phases updated to reflect Sprint 5 completion status. |

---

*End of document.*
