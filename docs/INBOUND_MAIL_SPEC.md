# EaaS Inbound Mail — Product & Technical Specification

## 1. Product Overview

### Problem Statement
EaaS currently only sends emails. Customers need to receive and process inbound emails — support replies, automated workflows triggered by incoming mail, and reply tracking for conversations.

### Use Cases
1. **Reply Tracking** — Customer sends transactional email, recipient replies, customer app receives the reply via webhook
2. **Support Inbox** — Route inbound emails to specific addresses (support@, billing@) and forward to customer systems
3. **Auto-forwarding** — Forward received emails to configured endpoints or email addresses
4. **Webhook on Receive** — Real-time notification when an email arrives, with parsed content
5. **Inbound Email API** — Query and retrieve received emails via REST API

### Target Users
- SaaS apps that need reply handling (ticketing, CRM, helpdesk)
- Developers building email-driven workflows (parse incoming data, trigger actions)
- Businesses consolidating email processing into one API

---

## 2. Technical Architecture

### Inbound Email Flow

```
Sender --> MX Record --> AWS SES Inbound
                              |
                         SES Receipt Rule
                              |
                    +---------+---------+
                    |                   |
                S3 Bucket          SNS Topic
                (raw MIME)              |
                                  SNS --> WebhookProcessor
                                              |
                                         Parse MIME (MimeKit)
                                              |
                                    +----+----+----+
                                    |    |    |    |
                                   DB  Redis  S3   Webhook
                              (metadata) (cache) (attachments) (dispatch)
```

### AWS Services Required
- **SES Inbound Receiving** — Receive emails on verified domains
- **S3** — Store raw MIME messages and attachments
- **SNS** — Notify EaaS when new email arrives

### Processing Pipeline
1. Email arrives at MX record pointing to AWS SES
2. SES Receipt Rule stores raw MIME in S3 and publishes SNS notification
3. WebhookProcessor receives SNS notification
4. Worker fetches raw MIME from S3
5. MimeKit parses headers, body (HTML + text), attachments
6. Metadata saved to PostgreSQL (`inbound_emails` table)
7. Attachments saved to S3 with references in `inbound_attachments` table
8. Webhook dispatched to customer endpoint with parsed email data
9. Redis caches recent inbound emails for fast dashboard access

---

## 3. Database Schema

### Table: `inbound_emails`

```sql
CREATE TABLE inbound_emails (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    message_id      VARCHAR(255) NOT NULL,          -- RFC Message-ID header
    from_email      VARCHAR(255) NOT NULL,
    from_name       VARCHAR(255),
    to_emails       JSONB NOT NULL,                 -- [{email, name}]
    cc_emails       JSONB DEFAULT '[]',
    bcc_emails      JSONB DEFAULT '[]',
    reply_to        VARCHAR(255),
    subject         VARCHAR(1024),
    html_body       TEXT,
    text_body       TEXT,
    headers         JSONB,                          -- all parsed headers
    tags            TEXT[] DEFAULT '{}',
    metadata        JSONB DEFAULT '{}',
    status          VARCHAR(20) NOT NULL DEFAULT 'received',  -- received, processed, forwarded, failed
    s3_key          VARCHAR(512),                   -- raw MIME location in S3
    spam_score      DECIMAL(5,2),
    spam_verdict    VARCHAR(20),                    -- pass, fail, unknown
    virus_verdict   VARCHAR(20),                    -- pass, fail, unknown
    spf_verdict     VARCHAR(20),
    dkim_verdict    VARCHAR(20),
    dmarc_verdict   VARCHAR(20),
    in_reply_to     VARCHAR(255),                   -- for thread tracking
    references      TEXT,                           -- References header for threading
    outbound_email_id UUID REFERENCES emails(id),   -- link to original sent email if reply
    received_at     TIMESTAMP NOT NULL DEFAULT NOW(),
    processed_at    TIMESTAMP,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_inbound_emails_tenant ON inbound_emails(tenant_id);
CREATE INDEX idx_inbound_emails_tenant_received ON inbound_emails(tenant_id, received_at DESC);
CREATE INDEX idx_inbound_emails_from ON inbound_emails(tenant_id, from_email);
CREATE INDEX idx_inbound_emails_message_id ON inbound_emails(message_id);
CREATE INDEX idx_inbound_emails_in_reply_to ON inbound_emails(in_reply_to);
CREATE INDEX idx_inbound_emails_outbound ON inbound_emails(outbound_email_id);
```

### Table: `inbound_attachments`

```sql
CREATE TABLE inbound_attachments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    inbound_email_id UUID NOT NULL REFERENCES inbound_emails(id) ON DELETE CASCADE,
    filename        VARCHAR(255) NOT NULL,
    content_type    VARCHAR(100) NOT NULL,
    size_bytes      BIGINT NOT NULL,
    s3_key          VARCHAR(512) NOT NULL,
    content_id      VARCHAR(255),                   -- for inline images (CID)
    is_inline       BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_inbound_attachments_email ON inbound_attachments(inbound_email_id);
```

### Table: `inbound_rules`

```sql
CREATE TABLE inbound_rules (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    name            VARCHAR(100) NOT NULL,
    domain_id       UUID NOT NULL REFERENCES sending_domains(id),
    match_pattern   VARCHAR(255) NOT NULL,          -- e.g., "support@", "*@", "billing@"
    action          VARCHAR(20) NOT NULL,           -- webhook, forward, store
    webhook_url     VARCHAR(2048),                  -- for action=webhook
    forward_to      VARCHAR(255),                   -- for action=forward
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    priority        INT NOT NULL DEFAULT 0,         -- lower = higher priority
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_inbound_rules_tenant ON inbound_rules(tenant_id);
CREATE INDEX idx_inbound_rules_domain ON inbound_rules(domain_id);
```

---

## 4. Domain Entities

### New Entities (src/EaaS.Domain/Entities/)

```csharp
// InboundEmail.cs
public class InboundEmail
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string MessageId { get; set; }
    public string FromEmail { get; set; }
    public string? FromName { get; set; }
    public string ToEmails { get; set; }        // JSON
    public string CcEmails { get; set; }
    public string? ReplyTo { get; set; }
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public string? Headers { get; set; }        // JSON
    public string[] Tags { get; set; }
    public string? S3Key { get; set; }
    public string? SpamVerdict { get; set; }
    public string? VirusVerdict { get; set; }
    public string? SpfVerdict { get; set; }
    public string? DkimVerdict { get; set; }
    public string? DmarcVerdict { get; set; }
    public string? InReplyTo { get; set; }
    public Guid? OutboundEmailId { get; set; }
    public InboundEmailStatus Status { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; }
    public Email? OutboundEmail { get; set; }
    public ICollection<InboundAttachment> Attachments { get; set; }
}

// InboundAttachment.cs
public class InboundAttachment
{
    public Guid Id { get; set; }
    public Guid InboundEmailId { get; set; }
    public string Filename { get; set; }
    public string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string S3Key { get; set; }
    public string? ContentId { get; set; }
    public bool IsInline { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public InboundEmail InboundEmail { get; set; }
}

// InboundRule.cs
public class InboundRule
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DomainId { get; set; }
    public string Name { get; set; }
    public string MatchPattern { get; set; }
    public InboundRuleAction Action { get; set; }
    public string? WebhookUrl { get; set; }
    public string? ForwardTo { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; }
    public SendingDomain Domain { get; set; }
}
```

### New Enums

```csharp
public enum InboundEmailStatus { Received, Processing, Processed, Forwarded, Failed }
public enum InboundRuleAction { Webhook, Forward, Store }
```

---

## 5. API Endpoints

### Inbound Rules (CRUD)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/inbound/rules` | Create inbound rule |
| `GET` | `/api/v1/inbound/rules` | List rules for tenant |
| `GET` | `/api/v1/inbound/rules/{id}` | Get rule by ID |
| `PUT` | `/api/v1/inbound/rules/{id}` | Update rule |
| `DELETE` | `/api/v1/inbound/rules/{id}` | Delete rule |

#### Create Rule Request
```json
{
  "name": "Support Inbox",
  "domainId": "uuid",
  "matchPattern": "support@",
  "action": "webhook",
  "webhookUrl": "https://myapp.com/webhooks/email",
  "priority": 0
}
```

### Inbound Emails (Read-only)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/inbound/emails` | List received emails (paginated, filtered) |
| `GET` | `/api/v1/inbound/emails/{id}` | Get email detail with attachments |
| `GET` | `/api/v1/inbound/emails/{id}/raw` | Download raw MIME from S3 |
| `GET` | `/api/v1/inbound/emails/{id}/attachments/{attachmentId}` | Download attachment from S3 |
| `DELETE` | `/api/v1/inbound/emails/{id}` | Delete inbound email |

#### List Query Parameters
- `page`, `pageSize` — pagination
- `from` — filter by sender
- `to` — filter by recipient
- `dateFrom`, `dateTo` — date range
- `status` — received/processed/failed
- `hasAttachments` — boolean filter

#### Inbound Email Webhook Payload (dispatched to customer)
```json
{
  "event": "email.received",
  "data": {
    "id": "uuid",
    "messageId": "<rfc-message-id>",
    "from": { "email": "sender@example.com", "name": "John Doe" },
    "to": [{ "email": "support@customer.com", "name": "" }],
    "cc": [],
    "subject": "Re: Order #12345",
    "textBody": "plain text content",
    "htmlBody": "<p>HTML content</p>",
    "headers": { "In-Reply-To": "<original-message-id>", "References": "..." },
    "attachments": [
      { "filename": "invoice.pdf", "contentType": "application/pdf", "sizeBytes": 45000, "downloadUrl": "/api/v1/inbound/emails/{id}/attachments/{attId}" }
    ],
    "spamVerdict": "pass",
    "virusVerdict": "pass",
    "receivedAt": "2026-04-01T12:00:00Z",
    "inReplyTo": "<original-message-id>",
    "outboundEmailId": "uuid-if-reply-to-sent-email"
  },
  "timestamp": "2026-04-01T12:00:01Z"
}
```

---

## 6. New Services & Interfaces

### Domain Interfaces
```csharp
// IInboundEmailStorage.cs — S3 operations
public interface IInboundEmailStorage
{
    Task<string> StoreRawEmailAsync(Guid tenantId, Guid emailId, Stream mimeStream, CancellationToken ct);
    Task<Stream> GetRawEmailAsync(string s3Key, CancellationToken ct);
    Task<string> StoreAttachmentAsync(Guid tenantId, Guid emailId, string filename, Stream content, CancellationToken ct);
    Task<Stream> GetAttachmentAsync(string s3Key, CancellationToken ct);
    Task DeleteEmailAsync(string s3Key, CancellationToken ct);
}

// IInboundEmailParser.cs — MIME parsing
public interface IInboundEmailParser
{
    InboundParsedEmail Parse(Stream mimeStream);
}
```

### Infrastructure Services
```
src/EaaS.Infrastructure/Services/S3InboundEmailStorage.cs  — AWS S3 implementation
src/EaaS.Infrastructure/Services/MimeKitInboundParser.cs   — MimeKit MIME parser
```

### New MassTransit Messages
```csharp
// ProcessInboundEmailMessage.cs
public class ProcessInboundEmailMessage
{
    public Guid TenantId { get; set; }
    public string S3BucketName { get; set; }
    public string S3ObjectKey { get; set; }
    public string SesMessageId { get; set; }
    public SesReceiptData Receipt { get; set; }  // spam/virus/spf/dkim/dmarc verdicts
}
```

### New Consumer
```
src/EaaS.Infrastructure/Messaging/InboundEmailConsumer.cs
```
Pipeline: Fetch from S3 -> Parse MIME -> Match rules -> Store metadata -> Dispatch webhook

---

## 7. Infrastructure Requirements

### AWS Configuration
```
1. SES Receipt Rule Set (per region)
   - Rule: match verified domains
   - Actions: S3 (store raw email) + SNS (notify)

2. S3 Bucket: eaas-inbound-emails-{env}
   - Lifecycle: 90-day retention (configurable)
   - Encryption: AES-256

3. SNS Topic: eaas-inbound-notifications
   - Subscription: HTTPS to WebhookProcessor /webhooks/ses/inbound

4. MX Records (per customer domain):
   - 10 inbound-smtp.{region}.amazonaws.com
```

### New Configuration
```json
{
  "Inbound": {
    "S3BucketName": "eaas-inbound-emails",
    "S3Region": "eu-west-1",
    "MaxEmailSizeMb": 30,
    "RetentionDays": 90,
    "Enabled": true
  }
}
```

### Docker Changes
- No new services needed — WebhookProcessor handles SNS, Worker processes emails
- Add `AWSSDK.S3` NuGet package to Infrastructure

---

## 8. Sprint Plan

### Sprint 4: Inbound Foundation (2 weeks)

| Story | Points | Priority |
|-------|--------|----------|
| Create InboundEmail, InboundAttachment, InboundRule entities + EF configurations | 3 | P0 |
| Database migration script for 3 new tables | 2 | P0 |
| Add InboundEmailStatus, InboundRuleAction enums | 1 | P0 |
| Implement IInboundEmailParser with MimeKit | 5 | P0 |
| Implement IInboundEmailStorage with S3 | 5 | P0 |
| Create InboundEmailConsumer (MassTransit) | 8 | P0 |
| Add SNS inbound endpoint to WebhookProcessor | 5 | P0 |
| Inbound Rules CRUD API (5 endpoints) | 5 | P0 |
| Reply tracking: match In-Reply-To to outbound emails | 3 | P1 |
| **Total** | **37** | |

### Sprint 5: API + Dashboard + Webhooks (2 weeks)

| Story | Points | Priority |
|-------|--------|----------|
| Inbound Emails list/detail API (5 endpoints) | 5 | P0 |
| Raw email download endpoint (S3 presigned URL) | 3 | P0 |
| Attachment download endpoint | 3 | P0 |
| Webhook dispatch for email.received event | 5 | P0 |
| Forward action implementation (re-send via SES) | 5 | P1 |
| Dashboard: Inbound emails list page | 5 | P1 |
| Dashboard: Inbound email detail page | 5 | P1 |
| Dashboard: Inbound rules management page | 5 | P1 |
| Inbound analytics (received/processed/failed counts) | 3 | P2 |
| **Total** | **39** | |

### Sprint 6: Production Hardening (1 week)

| Story | Points | Priority |
|-------|--------|----------|
| S3 lifecycle policies + retention cleanup | 3 | P0 |
| Spam/virus filtering with configurable thresholds | 3 | P1 |
| Rate limiting for inbound (per domain) | 2 | P1 |
| E2E tests: send email -> receive inbound -> webhook | 5 | P0 |
| Documentation: API docs, setup guide | 3 | P1 |
| **Total** | **16** | |

---

## 9. Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Email reception | AWS SES Inbound | Already on SES for outbound, single vendor, no self-hosted SMTP |
| Raw email storage | S3 | Cheap, scalable, lifecycle policies for retention |
| MIME parsing | MimeKit | Already in the project, battle-tested .NET MIME library |
| Attachment storage | S3 (separate prefix) | Same bucket, different key prefix per tenant |
| Processing | Async via MassTransit | Consistent with outbound pipeline, retry + dead letter |
| Thread tracking | In-Reply-To + References headers | RFC standard, links replies to original outbound emails |

---

## 10. Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Email reception | AWS SES Inbound | Already on SES for outbound, single vendor, no self-hosted SMTP needed |
| Raw email storage | S3 | MIME messages can be 10-25MB; PostgreSQL stores only parsed metadata |
| MIME parsing | MimeKit | Already a dependency, battle-tested .NET library |
| Processing service | Reuse WebhookProcessor | No new Docker service; I/O-bound work shares 128MB allocation |
| SNS endpoint | Separate `/webhooks/sns/inbound` | Different JSON schema from outbound notifications |
| Attachment downloads | Presigned S3 URLs (302 redirect) | Avoids streaming large files through API container |
| Reply tracking | In-Reply-To + References headers | RFC standard, links replies to original outbound emails |

## 11. Files to Create

### Domain Layer
- `src/EaaS.Domain/Entities/InboundEmail.cs`
- `src/EaaS.Domain/Entities/InboundAttachment.cs`
- `src/EaaS.Domain/Entities/InboundRule.cs`
- `src/EaaS.Domain/Enums/InboundEmailStatus.cs`
- `src/EaaS.Domain/Enums/InboundRuleAction.cs`
- `src/EaaS.Domain/Interfaces/IInboundEmailStorage.cs`
- `src/EaaS.Domain/Interfaces/IInboundEmailParser.cs`

### Infrastructure Layer
- `src/EaaS.Infrastructure/Persistence/Configurations/InboundEmailConfiguration.cs`
- `src/EaaS.Infrastructure/Persistence/Configurations/InboundAttachmentConfiguration.cs`
- `src/EaaS.Infrastructure/Persistence/Configurations/InboundRuleConfiguration.cs`
- `src/EaaS.Infrastructure/Services/S3InboundEmailStorage.cs`
- `src/EaaS.Infrastructure/Services/MimeKitInboundParser.cs`
- `src/EaaS.Infrastructure/Messaging/Contracts/ProcessInboundEmailMessage.cs`
- `src/EaaS.Infrastructure/Messaging/InboundEmailConsumer.cs`
- `src/EaaS.Infrastructure/Configuration/InboundSettings.cs`
- `scripts/migrate_sprint4.sql`

### API Layer
- `src/EaaS.Api/Features/Inbound/Rules/` (CRUD: 5 commands/queries + handlers + endpoints + validators)
- `src/EaaS.Api/Features/Inbound/Emails/` (List, Get, GetRaw, GetAttachment, Delete)

### WebhookProcessor
- `src/EaaS.WebhookProcessor/Handlers/InboundEmailHandler.cs` (SNS notification handler)

### Dashboard
- `dashboard/src/app/inbound/page.tsx`
- `dashboard/src/app/inbound/[id]/page.tsx`
- `dashboard/src/app/inbound/rules/page.tsx`
- `dashboard/src/components/inbound/` (email list, detail, rule form components)
