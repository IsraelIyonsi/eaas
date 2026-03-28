# EaaS - Sprint 2 Technical Specification

**Version:** 1.0
**Date:** 2026-03-27
**Author:** Senior Architect
**Reviewer:** Staff Engineer (Gate 1)
**Sprint:** 2 (Enhanced)
**Scope:** 15 stories, 47 story points
**Status:** Ready for Developer Handoff

---

## Table of Contents

1. [Current State Summary](#1-current-state-summary)
2. [Feature Specifications](#2-feature-specifications)
3. [Database Migrations](#3-database-migrations)
4. [Staff Engineer Review (Gate 1)](#4-staff-engineer-review-gate-1)
5. [Implementation Order](#5-implementation-order)

---

## 1. Current State Summary

Sprint 1 delivered a working email API with:
- 20+ endpoints (emails, templates, domains, API keys)
- MassTransit + RabbitMQ async email processing via `SendEmailConsumer`
- AWS SES v2 integration using `SendEmailRequest` (simple message format)
- PostgreSQL 16 with EF Core, Redis caching, API key auth
- Email entity already has `BatchId`, `CcEmails`, `BccEmails`, `Attachments`, `TrackOpens`, `TrackClicks`, `OpenedAt`, `ClickedAt` columns (defined in Sprint 1 schema but unused)
- `ApiKeyStatus` enum already includes `Rotating` value
- `EventType` enum already includes `Opened`, `Clicked` values
- `SuppressionReason` enum already includes `HardBounce`, `SoftBounceLimit`, `Complaint`, `Manual` values
- `SendEmailMessage` contract currently lacks CC/BCC, attachments, and tracking fields
- SES service uses `SendEmailAsync` with `Simple` message format (no raw email support yet)
- WebhookProcessor project is defined in architecture but not yet implemented

**Key implication:** Most of the schema is already in place. Sprint 2 is primarily about filling in the unused columns and building the processing logic.

---

## 2. Feature Specifications

### 2.1 CC/BCC Support (US-1.5) -- 2 pts

**What changes:**

**A. API Layer -- `SendEmailCommand` / `SendEmailHandler`**
- Add `Cc` and `Bcc` properties to `SendEmailCommand` as `List<string>`.
- In `SendEmailValidator`: combined count of `To + Cc + Bcc` must not exceed 50.
- In `SendEmailHandler`:
  - Check suppression for ALL recipients (to + cc + bcc) before enqueuing.
  - Serialize CC/BCC into the `Email` entity's existing `CcEmails` / `BccEmails` columns.
- Update `SendEmailEndpoint` request DTO to accept `cc` and `bcc` arrays.

**B. Message Contract -- `SendEmailMessage`**
- Add properties:
  ```csharp
  public string CcEmails { get; init; } = "[]";   // JSON array of emails
  public string BccEmails { get; init; } = "[]";   // JSON array of emails
  ```

**C. Consumer -- `SendEmailConsumer`**
- Deserialize CC/BCC from the message.
- Pass CC and BCC lists to the delivery service.

**D. SES Service -- `IEmailDeliveryService` / `SesEmailService`**
- Update `SendEmailAsync` signature:
  ```csharp
  Task<SendEmailResult> SendEmailAsync(
      string from,
      IReadOnlyList<string> recipients,
      IReadOnlyList<string>? ccRecipients,
      IReadOnlyList<string>? bccRecipients,
      string subject,
      string? htmlBody,
      string? textBody,
      CancellationToken cancellationToken = default);
  ```
- In `SesEmailService`, set `Destination.CcAddresses` and `Destination.BccAddresses` on the SES request.

**E. Response**
- `GetEmailResponse` should include `cc` and `bcc` arrays (BCC only visible to sender, which is fine since the API is authenticated per-tenant).

---

### 2.2 Swagger/OpenAPI Documentation (US-0.7) -- 3 pts

**What changes:**

**A. Program.cs -- Swagger Configuration**
- Replace basic `AddSwaggerGen()` with a configured version:
  ```csharp
  builder.Services.AddSwaggerGen(options =>
  {
      options.SwaggerDoc("v1", new OpenApiInfo
      {
          Title = "EaaS API",
          Version = "v1",
          Description = "Email as a Service - Transactional Email API"
      });

      // API key auth scheme
      options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
      {
          Type = SecuritySchemeType.Http,
          Scheme = "bearer",
          BearerFormat = "API Key",
          Description = "Enter your API key"
      });

      options.AddSecurityRequirement(new OpenApiSecurityRequirement
      {
          {
              new OpenApiSecurityScheme
              {
                  Reference = new OpenApiReference
                  {
                      Type = ReferenceType.SecurityScheme,
                      Id = "Bearer"
                  }
              },
              Array.Empty<string>()
          }
      });

      // XML comments
      var xmlFile = $"{Assembly.GetExecutingAssembly().GetFriendlyName()}.xml";
      var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
      if (File.Exists(xmlPath))
          options.IncludeXmlComments(xmlPath);
  });
  ```

**B. Project File**
- Enable XML documentation generation in `EaaS.Api.csproj`:
  ```xml
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
  ```

**C. Endpoint Annotations**
- Add `.WithName()`, `.WithSummary()`, `.WithDescription()`, `.Produces<T>()`, `.ProducesValidationProblem()` to every endpoint mapping.
- Add `[EndpointSummary]` and `[EndpointDescription]` attributes where Minimal API supports them.
- Group endpoints by `.WithTags()` (already done for most groups).

**D. Request/Response Examples**
- Add `/// <summary>` XML comments to all request/response DTOs and their properties.
- Use `[SwaggerSchema]` attributes for example values where needed.

**E. Swagger UI availability**
- Remove the `IsDevelopment()` guard so Swagger is available in all environments (or make it configurable via `appsettings.json`).

---

### 2.3 Template Preview (US-2.5) -- 2 pts

**What changes:**

**A. New Endpoint:** `POST /api/v1/templates/{id}/preview`

**Request Body:**
```json
{
  "variables": {
    "name": "John Doe",
    "order_id": "ORD-12345"
  }
}
```

**Response: 200 OK**
```json
{
  "success": true,
  "data": {
    "subject": "Your order ORD-12345 is confirmed",
    "html_body": "<html>..rendered HTML..</html>",
    "text_body": "Hello John Doe, your order..."
  }
}
```

**B. Implementation**
- New files: `PreviewTemplateEndpoint.cs`, `PreviewTemplateCommand.cs`, `PreviewTemplateHandler.cs`, `PreviewTemplateValidator.cs`
- Handler loads template from DB (check `DeletedAt == null`), calls `ITemplateRenderingService.RenderAsync()` with provided variables, returns rendered output.
- No email is created. No message is published. Read-only operation.

**C. Validation**
- Template must exist and not be soft-deleted.
- Variables object is optional (renders with empty variables if omitted).

---

### 2.4 Remove Domain (US-3.3) -- 2 pts

**What changes:**

**A. New Endpoint:** `DELETE /api/v1/domains/{id}`

**Response: 200 OK**
```json
{
  "success": true,
  "data": {
    "message": "Domain removed successfully"
  }
}
```

**B. Implementation**
- New files: `RemoveDomainEndpoint.cs`, `RemoveDomainCommand.cs`, `RemoveDomainHandler.cs`
- Handler:
  1. Load domain by ID and tenant.
  2. Check no emails are in `Queued` or `Sending` status for this domain (query `emails` where `from_email LIKE '%@{domain}'` and status in Queued/Sending). If pending emails exist, return 409 Conflict.
  3. Soft delete: set `DeletedAt` column on domain (add `DeletedAt` column -- see migrations).
  4. Optionally call SES `DeleteEmailIdentityAsync` to remove from SES.
- Return 404 if domain not found or already deleted.

**C. Domain Entity Update**
- Add `DeletedAt` (nullable `DateTime`) property to `Domain` entity.

---

### 2.5 API Key Rotation (US-5.2) -- 2 pts

**What changes:**

**A. New Endpoint:** `POST /api/v1/keys/{id}/rotate`

**Response: 200 OK**
```json
{
  "success": true,
  "data": {
    "key_id": "...",
    "api_key": "eaas_new_key_plaintext_shown_once",
    "prefix": "eaas_new",
    "old_key_expires_at": "2026-03-28T10:30:00Z",
    "created_at": "2026-03-27T10:30:00Z"
  }
}
```

**B. Implementation**
- New files: `RotateApiKeyEndpoint.cs`, `RotateApiKeyCommand.cs`, `RotateApiKeyHandler.cs`
- Handler:
  1. Load existing API key by ID and tenant. Must be `Active` status.
  2. Generate new random key, hash with SHA-256.
  3. Update existing key: set `Status = Rotating`, set `RotatingExpiresAt = DateTime.UtcNow.AddHours(24)`.
  4. Create new API key record with `Status = Active`, same `TenantId`, `Name` (appended " (rotated)"), `AllowedDomains`.
  5. Cache the new key hash in Redis; keep old key hash cached with TTL of 24h.
  6. Return new plaintext key (shown once).
- After 24h grace period, old key becomes invalid. Implement via:
  - In `ApiKeyAuthHandler`: when authenticating, if key matches a `Rotating` status key, check `RotatingExpiresAt`. If expired, reject. If not expired, allow.

**C. Entity Changes**
- Add `RotatingExpiresAt` (nullable `DateTime`) property to `ApiKey` entity.
- Add `ReplacedByKeyId` (nullable `Guid`) property to `ApiKey` entity for audit trail.

---

### 2.6 Email Logs API with Filtering (US-4.1) -- 3 pts

**What changes:**

The `ListEmails` endpoint already exists (`GET /api/v1/emails`). This story enhances it with advanced filtering.

**A. Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `status` | `string` | Filter by email status (queued, sending, delivered, bounced, failed) |
| `from` | `string` | Filter by sender email (partial match) |
| `to` | `string` | Filter by recipient email (partial match) |
| `date_from` | `datetime` | Emails created after this date |
| `date_to` | `datetime` | Emails created before this date |
| `tags` | `string` | Comma-separated tags (any match) |
| `template_id` | `guid` | Filter by template used |
| `batch_id` | `string` | Filter by batch |
| `page` | `int` | Page number (default 1) |
| `page_size` | `int` | Items per page (default 20, max 100) |
| `sort_by` | `string` | Column to sort (created_at, sent_at, status). Default: created_at |
| `sort_dir` | `string` | asc or desc. Default: desc |

**B. Implementation**
- Update `ListEmailsQuery` with all filter properties.
- Update `ListEmailsHandler` to build dynamic EF Core query with `.Where()` clauses based on provided filters.
- For tag filtering: use PostgreSQL array overlap operator (`&&`) via EF Core.
- For `to` recipient search: use EF Core JSON functions or raw SQL for JSONB contains.
- Add proper indexes (most already exist from Sprint 1 schema).

---

### 2.7 Batch Sending (US-1.2) -- 5 pts

**What changes:**

**A. New Endpoint:** `POST /api/v1/emails/batch`

**Request Body:**
```json
{
  "emails": [
    {
      "from": "sender@example.com",
      "to": ["recipient1@example.com"],
      "subject": "Hello",
      "html_body": "<p>Hi</p>"
    },
    {
      "from": "sender@example.com",
      "to": ["recipient2@example.com"],
      "template_id": "...",
      "variables": { "name": "Jane" }
    }
  ]
}
```

**Response: 202 Accepted**
```json
{
  "success": true,
  "data": {
    "batch_id": "batch_a1b2c3d4",
    "total": 2,
    "accepted": 2,
    "rejected": 0,
    "messages": [
      {
        "index": 0,
        "message_id": "eaas_abc123",
        "status": "queued"
      },
      {
        "index": 1,
        "message_id": "eaas_def456",
        "status": "queued"
      }
    ]
  }
}
```

**B. Implementation**
- New files: `SendBatchEndpoint.cs`, `SendBatchCommand.cs`, `SendBatchHandler.cs`, `SendBatchValidator.cs`, `SendBatchRequest.cs`, `SendBatchResponse.cs`
- Handler:
  1. Generate a `batch_id` (format: `batch_` + 8 alphanumeric).
  2. Rate limit check: count all emails in batch against the rate limit (1 API call = N emails).
  3. Validate each email independently (domain verified, recipients not suppressed).
  4. For each valid email: create `Email` entity with `BatchId` set, publish individual `SendEmailMessage` to queue.
  5. Collect results per-email: accepted or rejected with reason.
  6. Return aggregate response.
- Partial success is allowed: some emails can be accepted while others are rejected.

**C. Validation**
- `emails` array: min 1, max 100 items.
- Each item validated with the same rules as single send.
- Rate limit applies to the batch as a whole (100 emails/minute per key -- a batch of 50 counts as 50).

**D. Rate Limiting Adjustment**
- Update `SendEmailHandler` and `SendBatchHandler` to use a shared rate limit counter. For batch: increment by `emails.Count` instead of 1.
- Update `ICacheService.CheckRateLimitAsync` to accept an `increment` parameter (default 1).

---

### 2.8 Attachments (US-1.4) -- 3 pts

**What changes:**

**A. API Layer**
- Accept `attachments` array in send request:
  ```json
  {
    "attachments": [
      {
        "filename": "invoice.pdf",
        "content": "base64-encoded-content",
        "content_type": "application/pdf"
      }
    ]
  }
  ```
- Validation:
  - Max 10 attachments per email.
  - Per-file max: 10MB (after base64 decode).
  - Total attachments max: 25MB (after base64 decode).
  - Allowed content types: `application/pdf`, `image/png`, `image/jpeg`, `image/gif`, `text/plain`, `text/csv`, `application/zip`.
  - Filename max length: 255 chars, no path separators.

**B. Temporary Storage Strategy**
- In `SendEmailHandler`: decode base64, write to a temp directory (`/tmp/eaas-attachments/{emailId}/`), store metadata (filename, content_type, size, temp path) in the `Email.Attachments` JSONB column.
- The `SendEmailMessage` contract gets a new property:
  ```csharp
  public string Attachments { get; init; } = "[]";  // JSON: [{ filename, contentType, sizeBytes, tempPath }]
  ```

**C. Consumer -- `SendEmailConsumer`**
- When attachments are present, switch from `SendEmailAsync` (simple) to a new `SendRawEmailAsync` method.
- Build MIME message using **MimeKit**:
  1. Create `MimeMessage` with From, To, CC, BCC, Subject.
  2. Build `Multipart/mixed` body: text/html part + attachment parts.
  3. Read each attachment file from temp path, create `MimePart`.
  4. Serialize to `MemoryStream`.
- Call SES `SendEmailAsync` with `Raw` content (base64-encoded MIME).
- After successful send (or final failure), delete temp files.

**D. New Interface Method**
- Add to `IEmailDeliveryService`:
  ```csharp
  Task<SendEmailResult> SendRawEmailAsync(
      Stream mimeMessage,
      CancellationToken cancellationToken = default);
  ```
- Implement in `SesEmailService` using `SendEmailRequest` with `Content.Raw`.

**E. NuGet Addition**
- Add `MimeKit` package to `EaaS.Infrastructure`.

**F. Cleanup**
- Add a `finally` block in the consumer to delete temp files regardless of success/failure.
- Consider a background cleanup job for orphaned temp files older than 1 hour.

---

### 2.9 SNS Webhook Processing -- Bounce Handling (US-6.1) -- 5 pts

**What changes:**

**A. New Project Implementation: `EaaS.WebhookProcessor`**

This is a new Minimal API application that receives inbound webhooks from AWS SNS.

**Endpoint:** `POST /webhooks/sns`

**B. SNS Message Flow**
1. AWS SES publishes bounce/complaint/delivery events to an SNS topic.
2. SNS sends HTTP POST to our webhook endpoint.
3. First message is `SubscriptionConfirmation` -- must auto-confirm by GETting the `SubscribeURL`.
4. Subsequent messages are `Notification` type with SES event payload.

**C. SNS Message Validation**
- Verify message signature using AWS SNS certificate:
  1. Download signing certificate from `SigningCertURL` (cache it).
  2. Verify `SigningCertURL` is from `*.amazonaws.com`.
  3. Build the string-to-sign per SNS spec.
  4. Verify SHA1WithRSA signature.
- Reject any message with invalid signature (return 403).

**D. Bounce Processing (US-6.1)**
- Parse `bounceType` from SES notification:
  - **Permanent (hard bounce):** auto-suppress ALL bounced recipients immediately.
    - For each bounced recipient: insert into `suppression_list` with `reason = hard_bounce`, `source_message_id = email.MessageId`.
    - Update Redis suppression cache.
    - Update email status to `Bounced`.
    - Insert `EmailEvent` with `EventType.Bounced` and bounce details in `Data`.
  - **Transient (soft bounce):** log only, do NOT suppress.
    - Insert `EmailEvent` with bounce details for observability.
    - Do not change email status (SES may retry).

**E. Complaint Processing (US-6.2)**
- Parse complaint notification.
- Auto-suppress ALL complained recipients immediately.
  - Insert into `suppression_list` with `reason = complaint`.
  - Update Redis suppression cache.
  - Update email status to `Complained`.
  - Insert `EmailEvent` with `EventType.Complained`.

**F. Delivery Processing**
- Parse delivery notification.
- Update email: set `Status = Delivered`, `DeliveredAt = notification.delivery.timestamp`.
- Insert `EmailEvent` with `EventType.Delivered`.

**G. SES Message ID Correlation**
- SNS notifications include the SES Message-ID in `mail.messageId`.
- Look up email by `SesMessageId` column (already indexed via `idx_emails_message_id` on `message_id`).
- IMPORTANT: SES Message-ID is stored in `ses_message_id`, not `message_id`. Need an index on `ses_message_id`:
  ```sql
  CREATE INDEX idx_emails_ses_message_id ON emails(ses_message_id) WHERE ses_message_id IS NOT NULL;
  ```

**H. Docker Compose**
- Add `webhook-processor` service to `docker-compose.yml`.
- Expose on a different port (e.g., 5002).
- Share the same DB connection string and Redis.

**I. Configuration**
- Add SNS topic ARN and allowed source account to config.
- SES configuration set must be created with SNS destination for bounce/complaint/delivery events.

---

### 2.10 Complaint Auto-Suppression (US-6.2) -- 3 pts

Covered jointly with US-6.1 above. The `ComplaintProcessor` follows the same pattern as `BounceProcessor`. See section 2.9E.

---

### 2.11 Manual Suppression Management (US-6.3) -- 2 pts

**What changes:**

**A. New Endpoints:**

1. **`GET /api/v1/suppressions`** -- List suppressed addresses
   - Query params: `page`, `page_size`, `reason` (filter), `search` (email partial match)
   - Response: paginated list of `{ id, email_address, reason, source_message_id, suppressed_at }`

2. **`POST /api/v1/suppressions`** -- Manually suppress an address
   - Request: `{ "email_address": "user@example.com", "reason": "manual" }`
   - Validation: valid email format, not already suppressed.
   - Creates `SuppressionEntry` with `reason = Manual`, updates Redis cache.
   - Response: 201 Created with the suppression record.

3. **`DELETE /api/v1/suppressions/{id}`** -- Remove suppression
   - Removes from DB and Redis cache.
   - Response: 200 OK.

**B. Implementation**
- New feature folder: `Features/Suppressions/`
- Files: `ListSuppressionsEndpoint.cs`, `ListSuppressionsQuery.cs`, `ListSuppressionsHandler.cs`, `AddSuppressionEndpoint.cs`, `AddSuppressionCommand.cs`, `AddSuppressionHandler.cs`, `AddSuppressionValidator.cs`, `RemoveSuppressionEndpoint.cs`, `RemoveSuppressionCommand.cs`, `RemoveSuppressionHandler.cs`
- Add endpoint group in `Program.cs`:
  ```csharp
  var suppressionsGroup = app.MapGroup("/api/v1/suppressions")
      .RequireAuthorization()
      .WithTags("Suppressions");
  ```

---

### 2.12 Open Tracking (US-4.1/4.3) -- 5 pts

**What changes:**

**A. Tracking Token Generation**
- Create `TrackingTokenService` in `EaaS.Infrastructure`:
  ```csharp
  public interface ITrackingTokenService
  {
      string GenerateToken(Guid emailId, string eventType, string? originalUrl = null);
      TrackingTokenData? ValidateToken(string token);
  }
  ```
- Token format: HMAC-SHA256 of `{emailId}|{eventType}|{originalUrl}` using a secret key from configuration.
- Encode as URL-safe Base64.

**B. Pixel Injection in Worker/Consumer**
- After template rendering (if any) and before SES send, if `TrackOpens` is true:
  - Inject a 1x1 transparent pixel `<img>` tag before `</body>`:
    ```html
    <img src="https://hooks.email.israeliyonsi.dev/track/open/{token}" width="1" height="1" style="display:none" alt="" />
    ```
  - Token encodes `emailId + "open"`.

**C. Tracking Endpoint on WebhookProcessor**

**`GET /track/open/{token}`**
- Validate token via `ITrackingTokenService`.
- If valid:
  - Record `EmailEvent` with `EventType.Opened`, data includes user-agent and IP (from request headers).
  - Update email `OpenedAt` if null (first open only).
  - Return 1x1 transparent GIF (hardcoded 43-byte GIF binary) with `Content-Type: image/gif`, `Cache-Control: no-store`.
- If invalid: return the same GIF (don't reveal that tracking failed).

**D. Implementation Location**
- Pixel injection: `TrackingPixelInjector` service in `EaaS.Infrastructure`.
- Tracking endpoint: `TrackingEndpoint` in `EaaS.WebhookProcessor`.
- Both share `ITrackingTokenService` from Infrastructure.

---

### 2.13 Click Tracking (US-4.2/4.4) -- 5 pts

**What changes:**

**A. Link Rewriting in Worker/Consumer**
- After template rendering, if `TrackClicks` is true:
  - Parse HTML body, find all `<a href="...">` tags.
  - Skip mailto: links, unsubscribe links (containing "unsubscribe" in URL), and anchor (#) links.
  - For each qualifying link:
    - Generate tracking token encoding `emailId + "click" + originalUrl`.
    - Replace `href` with: `https://hooks.email.israeliyonsi.dev/track/click/{token}`
  - Use regex or an HTML parser (HtmlAgilityPack or AngleSharp) for reliable link rewriting.

**B. Tracking Endpoint on WebhookProcessor**

**`GET /track/click/{token}`**
- Validate token, extract original URL.
- If valid:
  - Record `EmailEvent` with `EventType.Clicked`, data includes original URL, user-agent, IP.
  - Update email `ClickedAt` if null (first click only).
  - Return 302 redirect to the original URL.
- If invalid: return 302 redirect to a configurable fallback URL (or 404).

**C. NuGet Addition**
- Add `AngleSharp` to `EaaS.Infrastructure` for robust HTML link parsing.

---

### 2.14 CI/CD Pipeline Activation (US-0.8) -- 3 pts

**What changes:**

Sprint 1 already created GitHub Actions workflow files and DevOps scripts. This story activates them for Sprint 2:

**A. Update Workflow**
- Ensure the workflow builds all projects including the new `EaaS.WebhookProcessor`.
- Add WebhookProcessor Dockerfile and service to docker-compose.
- Run all tests (unit + integration) in CI.

**B. Docker Images**
- Build and tag images for: `eaas-api`, `eaas-worker`, `eaas-webhook-processor`.
- Push to GitHub Container Registry on merge to `main`.

**C. Deployment**
- SSH deployment script pulls all three images.
- Webhook processor needs its own port mapping and potential public URL for SNS callbacks.

---

### 2.15 List/Search Templates (US-2.3) -- 2 pts

**What changes:**

The `ListTemplates` endpoint already exists. Enhance with search:

**A. Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `search` | `string` | Search by template name (partial match, case-insensitive) |
| `page` | `int` | Page number (default 1) |
| `page_size` | `int` | Items per page (default 20, max 100) |

**B. Implementation**
- Update `ListTemplatesQuery` with `search` parameter.
- Update handler to add `.Where(t => t.Name.Contains(search))` when search is provided.
- Already filters by `DeletedAt == null`.

---

## 3. Database Migrations

### 3.1 Analysis of Existing Schema vs. Required Changes

Most columns already exist from Sprint 1's forward-looking schema design:
- `emails.batch_id` -- EXISTS
- `emails.cc_emails` -- EXISTS
- `emails.bcc_emails` -- EXISTS
- `emails.attachments` -- EXISTS
- `emails.track_opens` -- EXISTS
- `emails.track_clicks` -- EXISTS
- `emails.opened_at` -- EXISTS
- `emails.clicked_at` -- EXISTS
- `suppression_list` table -- EXISTS with all columns
- `api_key_status` enum includes `rotating` -- EXISTS

### 3.2 Required Migrations

```sql
-- ============================================================
-- EaaS Sprint 2 Migration
-- ============================================================

-- -----------------------------------------------------------
-- 1. Add tracking_id to emails (for open/click token lookup)
-- -----------------------------------------------------------
ALTER TABLE emails
    ADD COLUMN tracking_id VARCHAR(64);

CREATE UNIQUE INDEX idx_emails_tracking_id
    ON emails(tracking_id)
    WHERE tracking_id IS NOT NULL;

-- -----------------------------------------------------------
-- 2. Add SES message ID index (for bounce/delivery correlation)
-- -----------------------------------------------------------
CREATE INDEX idx_emails_ses_message_id
    ON emails(ses_message_id)
    WHERE ses_message_id IS NOT NULL;

-- -----------------------------------------------------------
-- 3. Add domain soft-delete support
-- -----------------------------------------------------------
ALTER TABLE domains
    ADD COLUMN deleted_at TIMESTAMPTZ;

CREATE INDEX idx_domains_deleted
    ON domains(tenant_id, deleted_at)
    WHERE deleted_at IS NULL;

-- -----------------------------------------------------------
-- 4. Add API key rotation support
-- -----------------------------------------------------------
ALTER TABLE api_keys
    ADD COLUMN rotating_expires_at TIMESTAMPTZ,
    ADD COLUMN replaced_by_key_id UUID REFERENCES api_keys(id);

-- -----------------------------------------------------------
-- 5. Add tags index for filtering (GIN index for array overlap)
-- -----------------------------------------------------------
CREATE INDEX idx_emails_tags
    ON emails USING GIN (tags);

-- -----------------------------------------------------------
-- 6. Add composite index for email log filtering by date range
-- -----------------------------------------------------------
CREATE INDEX idx_emails_tenant_date_range
    ON emails(tenant_id, created_at DESC, status);

-- -----------------------------------------------------------
-- 7. Add index for suppression email search
-- -----------------------------------------------------------
CREATE INDEX idx_suppression_email_pattern
    ON suppression_list(tenant_id, email_address varchar_pattern_ops);
```

### 3.3 EF Core Migration Steps

1. Update `Domain` entity: add `DeletedAt` property.
2. Update `ApiKey` entity: add `RotatingExpiresAt`, `ReplacedByKeyId` properties.
3. Update `Email` entity: add `TrackingId` property.
4. Update EF Core configurations to map new columns.
5. Run `dotnet ef migrations add Sprint2_EnhancedFeatures`.
6. The migration should produce the SQL above (verify before applying).

---

## 4. Staff Engineer Review (Gate 1)

### 4.1 Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Temp file storage for attachments** | Medium | Container restarts lose temp files. Use a shared volume or process attachments synchronously in the API handler before enqueuing. Alternative: pass base64 content directly in the message (increases RabbitMQ message size). |
| **SNS signature verification complexity** | Medium | Use the AWS SNS Message Validator library (`Amazon.SimpleNotificationService` SDK has built-in helpers) rather than hand-rolling certificate verification. |
| **MimeKit raw email + SES size limits** | Low | SES has a 10MB raw message limit. With base64 encoding overhead, effective attachment limit is ~7.5MB. Our 25MB total limit exceeds this. **REQUIRED FIX: Lower total attachment limit to 7MB to account for MIME overhead and base64 inflation of the entire raw message.** |
| **Click tracking URL length** | Low | HMAC tokens with encoded URLs can get long. Use a lookup table instead of encoding the full URL in the token. |
| **Race condition on "first open/click"** | Low | Multiple simultaneous opens could both see `OpenedAt == null`. Use `UPDATE ... SET opened_at = @now WHERE opened_at IS NULL` (database-level atomicity). |

### 4.2 Architecture Concerns

1. **Attachment storage decision:** The spec proposes temp files, but this breaks in multi-instance deployments. **Recommendation:** For Sprint 2 (single VPS), temp files are acceptable. Document that Sprint 3 should migrate to S3/MinIO for attachment staging.

2. **Click tracking token bloat:** Encoding the original URL in the HMAC token makes tokens very long. **Recommendation:** Store `{emailId, originalUrl}` in a `tracking_links` table, generate a short random token as the lookup key. This also gives us click analytics per-link.

3. **WebhookProcessor shared DB access:** The webhook processor writes directly to the same DB as the API and Worker. This is fine for Sprint 2 scale but consider extracting event processing to MassTransit consumers in Sprint 3 for better decoupling.

4. **SES `SendEmail` vs `SendRawEmail`:** When attachments are present, we switch to raw email. When not, we stay with simple send. This dual-path increases testing surface. **Recommendation:** Accept the dual path -- the simple path is faster for the 95% case without attachments.

### 4.3 Scope Assessment

- 15 stories at 47 points in a 24-hour sprint is aggressive but achievable because:
  - Schema is already in place (minimal migration work).
  - CC/BCC, template preview, remove domain, template search, and Swagger are low-complexity.
  - The heavy items (tracking, bounce handling, attachments, batch) are well-specified.
- **If time runs short**, cut in this order (last = cut first):
  1. Cut click tracking (keep open tracking -- it's simpler)
  2. Cut attachments (most complex, SES raw email path)
  3. Cut CI/CD activation (already works from Sprint 1, just needs WebhookProcessor added)

### 4.4 Approval

**Gate 1 PASSED** with the following required changes incorporated into the spec above:
1. Lower total attachment size limit from 25MB to 7MB (SES raw message limit).
2. Use a tracking_links lookup table instead of encoding full URLs in click tokens.
3. Add `idx_emails_ses_message_id` index for bounce correlation.
4. Document temp file limitation for multi-instance (Sprint 3 action item).

---

## 5. Implementation Order

| # | Feature | Story IDs | Est. Hours | Dependencies | Rationale |
|---|---------|-----------|------------|--------------|-----------|
| 1 | **Database Migration** | -- | 1h | None | Foundation -- all features depend on new columns/indexes |
| 2 | **CC/BCC Support** | US-1.5 | 2h | Migration | Simplest feature, touches the send pipeline which others build on |
| 3 | **Swagger/OpenAPI Docs** | US-0.7 | 2h | None | Configuration-only, no business logic, validates all existing endpoints |
| 4 | **Template Search Enhancement** | US-2.3 | 1h | None | Minimal change to existing endpoint |
| 5 | **Template Preview** | US-2.5 | 1.5h | None | New endpoint, reuses existing template renderer |
| 6 | **Remove Domain** | US-3.3 | 1.5h | Migration | New endpoint, straightforward soft delete |
| 7 | **Email Logs API Filtering** | US-4.1 | 2.5h | Migration | Enhances existing endpoint with filtering |
| 8 | **Manual Suppression Management** | US-6.3 | 2h | None | New CRUD endpoints, standalone feature |
| 9 | **API Key Rotation** | US-5.2 | 2.5h | Migration | Touches auth pipeline, needs careful testing |
| 10 | **Batch Sending** | US-1.2 | 3h | CC/BCC (#2) | Reuses single-send logic, adds batch orchestration |
| 11 | **Tracking Token Service** | -- | 1.5h | Migration | Shared service needed by open + click tracking |
| 12 | **SNS Webhook Processor (Bounce/Delivery)** | US-6.1, US-6.2 | 4h | Migration | New project, SNS validation, bounce/complaint/delivery handling |
| 13 | **Open Tracking** | US-4.3 | 2.5h | Tracking Service (#11), Webhook Processor (#12) | Pixel injection + tracking endpoint |
| 14 | **Click Tracking** | US-4.4 | 3h | Tracking Service (#11), Webhook Processor (#12) | Link rewriting + redirect endpoint + tracking_links table |
| 15 | **Attachments** | US-1.4 | 3.5h | CC/BCC (#2) | Most complex: MimeKit, raw email, temp files, cleanup |
| 16 | **CI/CD Pipeline Activation** | US-0.8 | 1.5h | Webhook Processor (#12) | Add new service to build/deploy pipeline |

**Total estimated: ~35 hours of development time**

### Critical Path

```
Migration (#1) --> CC/BCC (#2) --> Batch (#10) --> Attachments (#15)
Migration (#1) --> Tracking Service (#11) --> Webhook Processor (#12) --> Open Tracking (#13) --> Click Tracking (#14)
```

The two chains can be parallelized if multiple developers are available. The critical path goes through the webhook processor chain at ~12 hours.

### Priority Tiers (if time-constrained)

**Tier 1 -- Must Ship (core value):**
Items 1-10 (migration, CC/BCC, Swagger, templates, domains, logs, suppressions, rotation, batch)

**Tier 2 -- High Value (tracking/webhooks):**
Items 11-13 (tracking service, webhook processor, open tracking)

**Tier 3 -- Can Defer:**
Items 14-16 (click tracking, attachments, CI/CD activation)
