# EaaS (Email as a Service) - Business & Product Requirements Document

**Version:** 1.0
**Date:** 2026-03-27
**Author:** Senior Business Analyst
**Owner:** Israel Iyonsi
**Status:** Draft

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

EaaS (Email as a Service) is a self-hosted transactional email API platform that replaces third-party SaaS email providers (Resend, SendGrid, Postmark) with fully owned infrastructure. The platform provides an HTTP API for sending transactional emails (invoices, notifications, confirmations, password resets) with built-in template rendering, delivery tracking, bounce/complaint handling, and analytics.

The system runs on a single Hetzner VPS using Docker Compose, leverages AWS SES for SMTP delivery at $0.10 per 1,000 emails, and provides a Next.js 15 dashboard for monitoring and management. Total operational cost is projected at $5-15/month, replacing $40+/month in recurring SaaS subscriptions.

This is strictly a transactional email platform. It does not include campaign management, mailing list management, drag-and-drop email editors, or any marketing email functionality.

---

## 2. Business Objectives

| # | Objective | Measurable Target |
|---|-----------|-------------------|
| 1 | **Reduce email infrastructure cost** | From $40+/month (Resend + SendGrid) to $5-15/month (75-85% savings) |
| 2 | **Own the infrastructure** | Zero vendor lock-in; full control over data, uptime, and feature roadmap |
| 3 | **Consolidate email sending** | Single API endpoint for all personal applications instead of multiple provider integrations |
| 4 | **Gain operational visibility** | Real-time delivery analytics, bounce rates, and open/click tracking across all apps |
| 5 | **Build portfolio asset** | Demonstrate full-stack engineering capability (.NET, PostgreSQL, Redis, RabbitMQ, Docker, AWS SES) |
| 6 | **Establish SaaS foundation** | Architecture that can scale to multi-tenant if demand warrants (Phase 3, optional) |

---

## 3. Problem Statement

### Current State

Israel operates multiple personal applications (including CashTrack) that send transactional emails to clients. Each application integrates directly with one or more SaaS email providers.

### Pain Points

1. **Recurring cost burden.** Resend charges $20/month for the Pro plan (50K emails). SendGrid charges $19.95/month for the Essentials plan. Combined spend exceeds $40/month for relatively low volume (<5K emails/month currently).

2. **Vendor lock-in.** Each provider has its own SDK, API format, template system, and dashboard. Switching providers requires code changes across every application.

3. **Fragmented visibility.** Delivery logs, bounce data, and analytics are scattered across multiple provider dashboards. There is no unified view of email health across all applications.

4. **Feature limitations on free/low tiers.** Free tiers impose daily sending limits, remove access to dedicated IPs, restrict template storage, and throttle API calls. Useful features like webhook notifications and advanced analytics sit behind higher pricing tiers.

5. **Data sovereignty concerns.** Email content, recipient lists, and delivery metadata are stored on third-party servers with no control over retention, access, or deletion policies.

6. **No customization.** Cannot modify bounce handling logic, retry strategies, rate limiting behavior, or template rendering pipelines. The provider decides how things work.

### Desired State

A single self-hosted API that all applications call to send transactional emails, with full ownership of the delivery pipeline, template system, analytics data, and operational dashboard.

---

## 4. Scope

### In Scope

- HTTP API for sending single and batch transactional emails
- Email template management with variable substitution (Liquid/Handlebars)
- File attachment support (up to 10MB per email, 25MB total)
- Domain verification management (SPF, DKIM, DMARC)
- Delivery tracking (sent, delivered, bounced, complained, opened, clicked)
- Bounce and complaint auto-suppression list
- API key management (create, revoke, rotate, scope per application)
- Webhook notifications to calling applications for delivery events
- Next.js 15 dashboard for logs, analytics, domain management, and template management
- Message queuing via RabbitMQ for reliable async delivery
- Redis caching for rate limiting, suppression list lookups, and template caching
- PostgreSQL for persistent storage of logs, templates, domains, API keys, and analytics
- Docker Compose deployment on Hetzner VPS
- AWS SES integration for SMTP delivery

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
| **Israel Iyonsi** | Owner, developer, primary user | Cost savings, infrastructure ownership, portfolio value |
| **Israel's application end-users** | Recipients of transactional emails | Reliable, timely delivery of invoices, notifications, and confirmations |
| **Future developer users (Phase 3)** | Potential SaaS customers | Affordable, reliable transactional email API with good DX |

---

## 6. Success Metrics

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| **Monthly infrastructure cost** | < $15/month | AWS billing + Hetzner invoice |
| **Email delivery rate** | > 98% | Dashboard analytics (delivered / sent) |
| **Bounce rate** | < 2% | Dashboard analytics (bounced / sent) |
| **API response time (p95)** | < 200ms for single send | Application logs + dashboard |
| **API response time (p95)** | < 500ms for batch send (up to 100) | Application logs + dashboard |
| **System uptime** | > 99.5% (< 3.65 hours downtime/month) | Health check monitoring |
| **Time to integrate new app** | < 30 minutes | Developer experience measurement |
| **Template render time (p95)** | < 50ms | Application logs |
| **Migration completion** | All apps moved off SaaS providers within 30 days of MVP | Manual tracking |
| **Suppression list accuracy** | 100% of hard bounces auto-suppressed | Log audit |

---

## 7. Competitive Analysis

### Pricing Comparison

| Feature | **EaaS (Self-Hosted)** | **Resend** | **SendGrid** | **Postmark** | **Mailgun** |
|---------|----------------------|------------|-------------|-------------|-------------|
| **Monthly base cost** | ~$5-15 (VPS + SES) | $0 (free) / $20 (Pro) | $0 (free) / $19.95 (Essentials) | $15 (10K emails) | $0 (free) / $35 (Foundation) |
| **Free tier emails** | N/A (pay SES only) | 3,000/month | 100/day | 100/month | 1,000/month |
| **Cost per 1K emails** | $0.10 (SES) | $0.00-$0.40 | $0.00-$0.50 | $1.25-$1.50 | $0.80-$1.00 |
| **Cost at 5K emails/month** | ~$5.35 | $0 (free tier) | $0 (free tier) | $15 | $0 (free tier) |
| **Cost at 50K emails/month** | ~$9.35 | $20 | $19.95 | $50 | $35 |
| **Cost at 200K emails/month** | ~$24.35 | $80 | $49.95 | $155 | $75 |
| **Data ownership** | Full | None | None | None | None |
| **Vendor lock-in** | None | High | High | High | High |

### Feature Comparison

| Feature | **EaaS** | **Resend** | **SendGrid** | **Postmark** | **Mailgun** |
|---------|---------|------------|-------------|-------------|-------------|
| **REST API** | Yes | Yes | Yes | Yes | Yes |
| **SMTP relay** | No (API only) | Yes | Yes | Yes | Yes |
| **Template management** | Yes | Yes | Yes (Dynamic Templates) | Yes | Yes |
| **Template rendering** | Liquid/Handlebars | React Email | Handlebars | Mustachio | Handlebars |
| **Batch sending** | Yes | Yes | Yes | Yes | Yes |
| **Attachments** | Yes | Yes | Yes | Yes | Yes |
| **Open tracking** | Yes | Yes | Yes | Yes | Yes |
| **Click tracking** | Yes | Yes | Yes | Yes | Yes |
| **Bounce handling** | Auto-suppression | Auto | Auto | Auto | Auto |
| **Webhooks** | Yes | Yes | Yes | Yes | Yes |
| **Analytics dashboard** | Yes (Next.js) | Yes | Yes | Yes | Yes |
| **Domain verification** | Yes | Yes | Yes | Yes | Yes |
| **Dedicated IP** | No (SES shared) | Paid add-on | Paid add-on | Included (some plans) | Paid add-on |
| **Custom retry logic** | Full control | No | No | No | No |
| **Self-hosted** | Yes | No | No | No | No |

### Competitive Advantages of EaaS

1. **Cost efficiency at any volume.** At current volumes (<5K/month), cost is ~$5/month vs. free tiers that impose restrictions. At scale (50K+/month), cost remains dramatically lower.
2. **Full data ownership.** All email content, metadata, and analytics stored on owned infrastructure.
3. **Zero vendor lock-in.** Applications integrate with a stable internal API. The underlying delivery provider (SES) can be swapped without touching application code.
4. **Custom logic.** Full control over retry policies, rate limiting, bounce handling, and template rendering.
5. **Portfolio value.** Demonstrates production-grade infrastructure engineering.

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
| 2 | **AWS SES account suspension** (complaint rate exceeds threshold) | Low | Critical | Implement hard bounce auto-suppression. Monitor complaint rate (must stay below 0.1%). Set up SES sending quotas and alarms. |
| 3 | **VPS downtime** (hardware failure, network outage) | Low | High | RabbitMQ persistence ensures messages survive restarts. Implement health checks with external monitoring (UptimeRobot). Daily PostgreSQL backups to S3. |
| 4 | **Maintenance burden exceeds expectations** | Medium | Medium | Keep architecture simple. Use battle-tested libraries. Automate deployments with Docker Compose. Document everything. |
| 5 | **Security breach** (API key leaked, unauthorized access) | Low | Critical | API key hashing (never store plain text). Rate limiting per key. IP allowlisting option. HTTPS only. Regular dependency updates. |
| 6 | **AWS SES cost spike** (runaway sends, infinite loop in calling app) | Low | Medium | Per-application rate limits. Daily sending quota per API key. Budget alerts on AWS. Circuit breaker in the worker service. |
| 7 | **Data loss** (PostgreSQL corruption) | Low | High | Automated daily backups. Point-in-time recovery with WAL archiving. Backup verification script. |
| 8 | **Scaling limits** (single VPS cannot handle load) | Low (Phase 1) | Medium | Current VPS handles 10K+ emails/day easily. If needed, scale vertically or add worker nodes. RabbitMQ supports distributed consumers. |
| 9 | **SES region outage** | Very Low | High | Configure SES in a secondary region as fallback. Implement provider abstraction layer for easy switching. |

---

# PART II: PRODUCT REQUIREMENTS DOCUMENT (PRD)

---

## 9. Product Overview

EaaS is a self-hosted transactional email platform consisting of three core components:

1. **API Server** (.NET 8 Minimal API) - Accepts email send requests, manages templates/domains/API keys, and serves the dashboard.
2. **Worker Service** (.NET 8 Worker Service) - Consumes messages from RabbitMQ, renders templates, delivers emails via AWS SES, and processes delivery status webhooks from SES.
3. **Dashboard** (Next.js 15) - Web UI for viewing send logs, analytics, managing templates, domains, API keys, and the suppression list.

### Architecture Overview

```
[Calling App] --HTTP POST--> [API Server] --enqueue--> [RabbitMQ]
                                  |                         |
                                  |                    [Worker Service]
                                  |                         |
                              [PostgreSQL]           [AWS SES] --SMTP--> [Recipient]
                              [Redis]                    |
                                  |                 [SNS Webhook]
                              [Dashboard]                |
                                                    [Worker Service] --update--> [PostgreSQL]
```

### Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| API Server | .NET 8 Minimal API | HTTP API, authentication, request validation |
| Worker Service | .NET 8 Worker Service | Queue consumption, template rendering, SES delivery |
| Dashboard | Next.js 15 | Management UI, analytics, log viewer |
| Database | PostgreSQL 16 | Persistent storage (logs, templates, domains, keys) |
| Cache | Redis 7 | Rate limiting, suppression list cache, template cache |
| Message Queue | RabbitMQ 3.13 | Async email processing, retry with dead-letter queues |
| Email Delivery | AWS SES | SMTP delivery, bounce/complaint notifications via SNS |
| Containerization | Docker Compose | Single-command deployment |
| Hosting | Hetzner VPS (CX22) | 2 vCPU, 4GB RAM, 40GB SSD, ~$4.35/month |

---

## 10. User Personas

### Persona 1: Israel (Owner / Primary User)

| Attribute | Detail |
|-----------|--------|
| **Name** | Israel Iyonsi |
| **Role** | Senior .NET Engineer, application developer |
| **Technical skill** | Expert - 8+ years backend development |
| **Goals** | Reduce costs, own infrastructure, consolidate email sending, gain visibility |
| **Frustrations** | Paying $40+/month for low volume, fragmented dashboards, vendor lock-in |
| **Usage pattern** | Integrates EaaS API into 2-5 personal applications. Checks dashboard weekly. Manages templates monthly. |
| **Devices** | Desktop browser (Chrome/Edge), no mobile requirement |

### Persona 2: Application (Calling System)

| Attribute | Detail |
|-----------|--------|
| **Name** | CashTrack / Other Personal Apps |
| **Role** | Automated system making API calls |
| **Integration method** | HTTP POST to EaaS API with API key authentication |
| **Usage pattern** | Sends 50-500 emails/day across invoice notifications, payment confirmations, user notifications |
| **Requirements** | Reliable delivery, fast API response, template rendering, delivery status callbacks |

### Persona 3: Future Developer (Phase 3, Optional)

| Attribute | Detail |
|-----------|--------|
| **Name** | External Developer |
| **Role** | Indie developer or small team needing affordable transactional email |
| **Technical skill** | Intermediate to senior |
| **Goals** | Low-cost, simple API, good documentation, reliable delivery |
| **Usage pattern** | 1K-50K emails/month, manages own templates, monitors delivery |

---

## 11. User Stories

### Epic 1: Email Sending

#### US-1.1: Send a Single Email

**As a** developer integrating with EaaS,
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

---

### Epic 2: Template Management

#### US-2.1: Create an Email Template

**As a** developer,
**I want** to create and store email templates with variable placeholders,
**so that** I can reuse them across multiple email sends.

**Acceptance Criteria:**

1. API accepts POST `/api/v1/templates` with `name`, `subject_template`, `html_body`, `text_body` (optional), and `variables_schema` (JSON Schema for expected variables).
2. Template names must be unique per account.
3. Template is validated for syntax errors (Liquid template syntax).
4. Template is stored in PostgreSQL and cached in Redis.
5. API returns 201 Created with the `template_id`.
6. Maximum template size: 512KB.

#### US-2.2: Update an Email Template

**As a** developer,
**I want** to update an existing template,
**so that** I can iterate on email content without creating new templates.

**Acceptance Criteria:**

1. API accepts PUT `/api/v1/templates/{id}` with updatable fields.
2. The previous version is retained (version history, last 10 versions).
3. Redis cache is invalidated on update.
4. API returns 200 OK with the updated template.
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
6. Duplicate domain names are rejected with 409 Conflict.

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

1. GET `/api/v1/domains` returns all domains with status, DNS records, and verification timestamps.
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

1. GET `/api/v1/logs` returns paginated email logs with: `message_id`, `to`, `from`, `subject`, `status`, `sent_at`, `delivered_at`, `opened_at`, `clicked_at`.
2. Supports filtering by: `status` (queued, sent, delivered, bounced, complained, opened, clicked), `to`, `from`, `date_range`, `api_key_id`, `template_id`.
3. Supports sorting by `sent_at` (default desc).
4. Default pagination: 50 items, max 200 per page.
5. GET `/api/v1/logs/{message_id}` returns full detail including all status transitions with timestamps.
6. Logs are retained for 90 days (configurable).

#### US-4.2: View Delivery Analytics

**As a** developer,
**I want** to see aggregate delivery statistics,
**so that** I can monitor the health of my email sending.

**Acceptance Criteria:**

1. GET `/api/v1/analytics` returns aggregate stats for a given date range.
2. Metrics include: `total_sent`, `total_delivered`, `total_bounced`, `total_complained`, `total_opened`, `total_clicked`, `delivery_rate`, `open_rate`, `click_rate`, `bounce_rate`, `complaint_rate`.
3. Supports grouping by: `day`, `week`, `month`.
4. Supports filtering by: `domain`, `api_key_id`, `template_id`.
5. Default date range: last 30 days.
6. Response is optimized for charting (array of time-series data points).

#### US-4.3: Track Email Opens

**As a** developer,
**I want** to know when recipients open my emails,
**so that** I can understand engagement.

**Acceptance Criteria:**

1. Open tracking is enabled by default (configurable per-send via `track_opens: false`).
2. A 1x1 transparent tracking pixel is injected into the HTML body.
3. When the pixel is loaded, an `opened` event is recorded with timestamp and approximate geolocation (country level from IP).
4. Multiple opens are recorded but `first_opened_at` is tracked separately.
5. Open tracking is only applied to HTML emails (not text-only).

#### US-4.4: Track Link Clicks

**As a** developer,
**I want** to know when recipients click links in my emails,
**so that** I can understand which content drives action.

**Acceptance Criteria:**

1. Click tracking is enabled by default (configurable per-send via `track_clicks: false`).
2. Links in the HTML body are rewritten to pass through the EaaS tracking endpoint.
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

1. POST `/api/v1/api-keys` accepts `name` (e.g., "CashTrack Production") and optional `allowed_domains` (restrict which from-domains this key can use).
2. The full API key is returned only once at creation time. It is stored as a SHA-256 hash.
3. API key format: `eaas_live_` prefix + 40 random alphanumeric characters.
4. Key is immediately active upon creation.
5. Response includes `key_id` (public identifier), `name`, `created_at`, and `prefix` (first 8 chars for identification).

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

**As a** developer,
**I want** a dashboard home page showing key metrics at a glance,
**so that** I can quickly assess the health of my email sending.

**Acceptance Criteria:**

1. Dashboard home shows: total sent (today, 7d, 30d), delivery rate, bounce rate, complaint rate, open rate, click rate.
2. A line chart shows daily send volume for the last 30 days.
3. A status indicator shows: system health (API up/down, worker up/down, queue depth).
4. Recent alerts are shown (high bounce rate, complaint threshold, domain verification failures).
5. Page loads in under 2 seconds.

#### US-7.2: Email Log Viewer

**As a** developer,
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

**As a** developer,
**I want** to manage email templates through the dashboard,
**so that** I can create and edit templates without making API calls.

**Acceptance Criteria:**

1. Dashboard lists all templates with name, version, send count, last updated.
2. Template editor with syntax highlighting for Liquid/HTML.
3. Live preview panel that renders the template with sample variables.
4. Variable schema editor (define expected variables and their types).
5. Version history viewer (last 10 versions with diff).

#### US-7.4: Domain Manager

**As a** developer,
**I want** to manage sending domains through the dashboard,
**so that** I can add domains and check verification status visually.

**Acceptance Criteria:**

1. Dashboard lists all domains with verification status (color-coded: green=verified, yellow=pending, red=failed).
2. Adding a domain shows the required DNS records in a copyable format.
3. A "Verify Now" button triggers immediate verification.
4. Individual DNS record status is shown (SPF, DKIM, DMARC independently).

#### US-7.5: Analytics Dashboard

**As a** developer,
**I want** rich analytics charts in the dashboard,
**so that** I can visualize sending trends and email performance.

**Acceptance Criteria:**

1. Time-series chart: daily sends, deliveries, bounces, complaints over selected date range.
2. Pie/donut chart: delivery status breakdown.
3. Bar chart: top sending templates by volume.
4. Bar chart: top sending domains by volume.
5. Table: per-API-key sending statistics.
6. Date range selector with presets: today, 7d, 30d, 90d, custom.
7. All charts are interactive (hover for values, click to drill down).

#### US-7.6: Suppression List Manager

**As a** developer,
**I want** to manage the suppression list through the dashboard,
**so that** I can review and modify suppressed addresses visually.

**Acceptance Criteria:**

1. Dashboard shows suppressed addresses with reason, date, and source email.
2. Search by email address (partial match).
3. Bulk remove selected addresses.
4. Manual add with reason selection.
5. Export suppression list as CSV.

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
4. A test ping is sent to verify the URL is reachable.
5. Maximum 10 webhook endpoints.

#### US-8.2: Receive Webhook Notifications

**As a** developer,
**I want** my webhook endpoints to receive real-time notifications for email events,
**so that** I can update my application state accordingly.

**Acceptance Criteria:**

1. Webhooks are delivered as POST requests with JSON body containing: `event`, `message_id`, `timestamp`, `data` (event-specific payload).
2. Each webhook includes an `X-EaaS-Signature` header (HMAC-SHA256 of the body using the endpoint's secret).
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
4. Dashboard shows webhook delivery logs (last 100 deliveries per endpoint with status and response code).

---

## 12. Feature Prioritization

### P0 - MVP Launch (Month 1)

Must-have features to replace current SaaS providers.

| Feature | User Story | Rationale |
|---------|-----------|-----------|
| Single email send | US-1.1 | Core functionality |
| Send with template | US-1.3 | Critical for application integration |
| Send with attachments | US-1.4 | Required for invoice emails |
| Create/update/list templates | US-2.1, US-2.2, US-2.3 | Templates are the primary integration pattern |
| Add/verify domain | US-3.1, US-3.2, US-3.3 | Required before any sending |
| API key create/revoke | US-5.1, US-5.3 | Authentication is mandatory |
| Automatic bounce suppression | US-6.1 | Protects sender reputation |
| Automatic complaint suppression | US-6.2 | Prevents SES suspension |
| Basic send logs | US-4.1 | Minimum debugging capability |
| Dashboard overview | US-7.1 | Basic monitoring |
| Email log viewer | US-7.2 | Basic troubleshooting |

### P1 - Enhanced (Month 2-3)

Important features that improve operational quality.

| Feature | User Story | Rationale |
|---------|-----------|-----------|
| Batch email send | US-1.2 | Efficiency for bulk transactional sends |
| CC/BCC support | US-1.5 | Common email feature |
| Template preview | US-2.5 | Developer experience |
| Template delete/restore | US-2.4 | Template lifecycle |
| Delivery analytics | US-4.2 | Operational visibility |
| Open tracking | US-4.3 | Engagement metrics |
| Click tracking | US-4.4 | Engagement metrics |
| API key rotation | US-5.4 | Security hygiene |
| Suppression list management | US-6.3 | Operational control |
| Analytics dashboard | US-7.5 | Visual monitoring |
| Template manager UI | US-7.3 | Dashboard completeness |
| Domain manager UI | US-7.4 | Dashboard completeness |
| Suppression list UI | US-7.6 | Dashboard completeness |

### P2 - Future / Multi-Tenant (Month 4+)

Nice-to-have features and SaaS preparation.

| Feature | User Story | Rationale |
|---------|-----------|-----------|
| Webhook notifications | US-8.1, US-8.2, US-8.3 | Application integration |
| Remove domain | US-3.4 | Domain lifecycle |
| API key listing with stats | US-5.2 | Operational visibility |
| Multi-tenant account system | (Future) | SaaS preparation |
| Usage-based billing | (Future) | SaaS monetization |
| Public API documentation portal | (Future) | Developer onboarding |
| SDKs (.NET, Node.js, Python) | (Future) | Developer experience |
| IP warmup automation | (Future) | Deliverability at scale |
| Scheduled/delayed sends | (Future) | Advanced feature |
| Email content validation (spam score) | (Future) | Deliverability improvement |

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

### Security

| Requirement | Implementation |
|-------------|----------------|
| API authentication | API key in `Authorization: Bearer` header |
| API key storage | SHA-256 hashed, never stored in plaintext |
| Transport security | HTTPS only (TLS 1.2+), HSTS header |
| Rate limiting | 100 requests/second per API key (configurable) |
| Input validation | All inputs sanitized; parameterized SQL queries |
| Dashboard authentication | Username/password with bcrypt hashing (single-user in Phase 1) |
| Dashboard sessions | Secure, HttpOnly, SameSite=Strict cookies |
| Webhook signatures | HMAC-SHA256 for webhook payload verification |
| Secret management | AWS SES credentials in environment variables, not in code |
| Dependency security | Automated vulnerability scanning (Dependabot or similar) |
| CORS | Disabled (API is server-to-server, no browser calls) |

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

| Metric | Phase 1 Target | Growth Path |
|--------|---------------|-------------|
| Emails per day | 1,000 | Scale to 100K with same infrastructure |
| Emails per month | 30,000 | Scale to 3M with horizontal worker scaling |
| API keys | 10 | No practical limit |
| Templates | 100 | No practical limit |
| Domains | 10 | SES limit: 10,000 |
| Log retention | 90 days | Configurable, archive to S3 for long-term |
| Concurrent API connections | 100 | .NET handles 10K+ concurrent with Kestrel |

### Observability

| Requirement | Implementation |
|-------------|----------------|
| Structured logging | Serilog with JSON output |
| Log aggregation | Console output captured by Docker |
| Health check endpoint | GET `/health` returns API, DB, Redis, RabbitMQ, SES status |
| Metrics | Custom metrics exposed via dashboard (send rate, error rate, queue depth) |
| Alerting | Dashboard alerts for: high bounce rate, complaint threshold, queue backup, worker down |
| External monitoring | UptimeRobot or similar pinging `/health` every 60 seconds |

---

## 14. API Contract

### Authentication

All API requests must include the API key in the Authorization header:

```
Authorization: Bearer eaas_live_aBcDeFgH1234567890...
```

### Base URL

```
https://email.israeliyonsi.dev/api/v1
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
    "queued_at": "2026-03-27T10:30:00Z"
  }
}
```

---

#### POST /api/v1/emails/send (with template)

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
  "template_id": "tmpl_invoice_v2",
  "variables": {
    "client_name": "John Doe",
    "invoice_number": "INV-2026-001",
    "amount": "$500.00",
    "due_date": "2026-04-15",
    "payment_link": "https://cashtrack.app/pay/INV-2026-001"
  },
  "tags": ["invoice"],
  "metadata": {
    "invoice_id": "INV-2026-001"
  }
}
```

**Response (202 Accepted):**
```json
{
  "success": true,
  "data": {
    "message_id": "msg_x7y8z9w0v1u2",
    "status": "queued",
    "queued_at": "2026-03-27T10:31:00Z"
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

#### POST /api/v1/templates

**Request:**
```json
{
  "name": "payment_reminder",
  "subject_template": "Payment Reminder: {{ invoice_number }}",
  "html_body": "<!DOCTYPE html><html><body><h1>Hi {{ client_name }},</h1><p>This is a reminder that invoice <strong>{{ invoice_number }}</strong> for <strong>{{ amount }}</strong> is due on {{ due_date }}.</p><a href=\"{{ payment_link }}\">Pay Now</a></body></html>",
  "text_body": "Hi {{ client_name }},\n\nThis is a reminder that invoice {{ invoice_number }} for {{ amount }} is due on {{ due_date }}.\n\nPay here: {{ payment_link }}",
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
    "created_at": "2026-03-27T10:00:00Z"
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
        "value": "v=DMARC1; p=quarantine; rua=mailto:dmarc@cashtrack.app",
        "purpose": "DMARC"
      }
    ],
    "created_at": "2026-03-27T10:00:00Z"
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
    "api_key": "eaas_live_aBcDeFgH1234567890xYzWvUtSrQpOnMlKjIhG",
    "prefix": "eaas_liv",
    "allowed_domains": ["notifications.cashtrack.app"],
    "created_at": "2026-03-27T10:00:00Z"
  }
}
```

> **Warning:** The `api_key` field is returned only on creation. Store it securely. It cannot be retrieved again.

---

#### GET /api/v1/analytics

**Request:**
```
GET /api/v1/analytics?start_date=2026-03-01&end_date=2026-03-27&group_by=day
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "summary": {
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
    },
    "time_series": [
      {
        "date": "2026-03-01",
        "sent": 145,
        "delivered": 143,
        "bounced": 1,
        "complained": 0,
        "opened": 98,
        "clicked": 42
      }
    ]
  }
}
```

---

#### GET /health

**Response (200 OK):**
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

### Full Endpoint Summary

| Method | Endpoint | Description | Priority |
|--------|----------|-------------|----------|
| POST | `/api/v1/emails/send` | Send a single email | P0 |
| POST | `/api/v1/emails/batch` | Send batch emails | P1 |
| GET | `/api/v1/emails/{message_id}` | Get email status | P0 |
| GET | `/api/v1/templates` | List templates | P0 |
| GET | `/api/v1/templates/{id}` | Get template | P0 |
| POST | `/api/v1/templates` | Create template | P0 |
| PUT | `/api/v1/templates/{id}` | Update template | P0 |
| DELETE | `/api/v1/templates/{id}` | Delete template | P1 |
| PATCH | `/api/v1/templates/{id}/restore` | Restore template | P1 |
| POST | `/api/v1/templates/{id}/preview` | Preview template | P1 |
| GET | `/api/v1/domains` | List domains | P0 |
| GET | `/api/v1/domains/{id}` | Get domain | P0 |
| POST | `/api/v1/domains` | Add domain | P0 |
| POST | `/api/v1/domains/{id}/verify` | Verify domain | P0 |
| DELETE | `/api/v1/domains/{id}` | Remove domain | P2 |
| GET | `/api/v1/api-keys` | List API keys | P2 |
| POST | `/api/v1/api-keys` | Create API key | P0 |
| DELETE | `/api/v1/api-keys/{id}` | Revoke API key | P0 |
| POST | `/api/v1/api-keys/{id}/rotate` | Rotate API key | P1 |
| GET | `/api/v1/logs` | List email logs | P0 |
| GET | `/api/v1/logs/{message_id}` | Get email log detail | P0 |
| GET | `/api/v1/analytics` | Get analytics | P1 |
| GET | `/api/v1/suppressions` | List suppressions | P1 |
| POST | `/api/v1/suppressions` | Add suppression | P1 |
| DELETE | `/api/v1/suppressions/{email}` | Remove suppression | P1 |
| GET | `/api/v1/webhooks` | List webhooks | P2 |
| POST | `/api/v1/webhooks` | Create webhook | P2 |
| PUT | `/api/v1/webhooks/{id}` | Update webhook | P2 |
| DELETE | `/api/v1/webhooks/{id}` | Delete webhook | P2 |
| GET | `/health` | Health check | P0 |

---

## 15. Product Phases / Roadmap

### Phase 1: MVP (Weeks 1-4)

**Goal:** Replace all SaaS email providers. All current applications send through EaaS.

| Week | Deliverable |
|------|------------|
| Week 1 | Project setup: solution structure, Docker Compose, PostgreSQL schema, Redis, RabbitMQ configuration. Domain model and entity design. |
| Week 2 | Core API: email send endpoint, template CRUD, domain registration/verification, API key management. Queue producer. |
| Week 3 | Worker service: queue consumer, Liquid template rendering, SES integration, bounce/complaint handling via SNS. |
| Week 4 | Dashboard MVP: overview page, log viewer. Integration testing. Migrate first app (CashTrack). |

**Exit Criteria:**
- CashTrack sends all transactional emails through EaaS
- Delivery rate > 98%
- Bounce auto-suppression working
- Dashboard shows send logs and basic metrics

### Phase 2: Enhanced Platform (Weeks 5-10)

**Goal:** Full feature set for a production-grade transactional email platform.

| Week | Deliverable |
|------|------------|
| Week 5-6 | Batch sending, CC/BCC, template preview, template versioning, template soft-delete/restore. |
| Week 7-8 | Open tracking, click tracking, analytics API, analytics dashboard with charts. |
| Week 9 | API key rotation, suppression list management (API + dashboard), domain manager UI, template manager UI. |
| Week 10 | Hardening: load testing, security audit, documentation, backup automation, monitoring setup. |

**Exit Criteria:**
- All P0 and P1 features complete
- Open and click tracking operational
- Analytics dashboard with full charting
- All applications migrated off SaaS providers
- SaaS subscriptions cancelled

### Phase 3: Multi-Tenant SaaS (Future, Optional)

**Goal:** Open the platform to other developers as a paid service.

| Milestone | Deliverable |
|-----------|------------|
| 3.1 | Multi-tenant account system with isolated data. |
| 3.2 | User registration, authentication (OAuth), onboarding flow. |
| 3.3 | Usage-based billing (Stripe integration). |
| 3.4 | Public API documentation portal (OpenAPI/Swagger). |
| 3.5 | Client SDKs (.NET, Node.js, Python). |
| 3.6 | Webhook system (P2 features). |
| 3.7 | Marketing site and launch. |

**Decision gate:** Phase 3 proceeds only if there is validated demand from other developers.

---

## 16. Constraints & Assumptions

### Constraints

| # | Constraint | Impact |
|---|-----------|--------|
| 1 | **Single VPS deployment.** Hetzner CX22 (2 vCPU, 4GB RAM, 40GB SSD). | No horizontal scaling without infrastructure changes. Worker service is the bottleneck for throughput. |
| 2 | **AWS SES sending limits.** New SES accounts start in sandbox mode (200 emails/day). Production access must be requested. | Cannot send to unverified recipients until production access is granted. Plan 1-3 day approval wait. |
| 3 | **AWS SES rate limits.** Default: 14 emails/second in production. | Must implement send throttling in the worker to stay within limits. Can request increases. |
| 4 | **Single developer.** Israel is the sole developer and operator. | Development velocity limited. Prioritize ruthlessly. Automate operations where possible. |
| 5 | **Budget ceiling.** Total infrastructure cost must stay under $20/month. | Rules out dedicated IPs ($24.95/month), multi-server setups, and premium monitoring tools. |
| 6 | **.NET 8 ecosystem.** Technology stack is fixed. | Template engine must be .NET-compatible (Fluid for Liquid, Handlebars.NET). |

### Assumptions

| # | Assumption | Risk if Wrong |
|---|-----------|---------------|
| 1 | Monthly send volume will remain under 50K emails for the first 6 months. | VPS may need upgrade; SES costs may exceed budget. |
| 2 | AWS SES production access will be approved within 3 business days. | MVP launch is delayed. Mitigation: apply for production access immediately. |
| 3 | Existing applications can be migrated to use the EaaS API within 30 days. | Parallel SaaS subscription costs continue. |
| 4 | Hetzner VPS provides sufficient uptime (99.9% SLA). | Need fallback hosting plan. Mitigation: daily backups enable quick migration. |
| 5 | Israel's sender reputation is clean (no prior blacklisting). | Deliverability issues from day one. Mitigation: check blacklists before launch. |
| 6 | Liquid template syntax (via Fluid library) is sufficient for all template needs. | May need to switch to Handlebars.NET or custom engine. |
| 7 | RabbitMQ on a single node is reliable enough for Phase 1-2. | Message loss risk. Mitigation: persistent queues with disk storage. |
| 8 | Browser-based Next.js 15 dashboard is acceptable (no mobile requirement). | If mobile monitoring is needed, would require responsive design work. |

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
| **HMAC-SHA256** | Hash-based Message Authentication Code using SHA-256. Used to verify webhook payload authenticity. |
| **Rate limiting** | Restricting the number of API requests a client can make within a time window. |
| **API key** | A secret token used to authenticate API requests. Identifies which application is making the request. |
| **Hetzner** | A German hosting provider offering affordable cloud VPS instances. |
| **RabbitMQ** | An open-source message broker that implements AMQP. Used for reliable async message processing. |
| **Minimal API** | A .NET 8 approach for building HTTP APIs with minimal boilerplate code. |
| **Next.js 15** | A React framework for building full-stack web applications with server-side rendering and static generation. |
| **Docker Compose** | A tool for defining and running multi-container Docker applications using a YAML configuration file. |
| **HSTS** | HTTP Strict Transport Security. Forces browsers to use HTTPS. |
| **WAL** | Write-Ahead Logging. PostgreSQL's mechanism for ensuring data integrity and enabling point-in-time recovery. |

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-27 | Senior Business Analyst | Initial draft - complete BRD/PRD |

---

*End of document.*
