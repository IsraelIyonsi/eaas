# EaaS - Sprint 3 Technical Specification

**Version:** 1.0
**Date:** 2026-03-27
**Author:** Senior Architect
**Reviewer:** Staff Engineer (Gate 1)
**Sprint:** 3
**Scope:** 10 stories, 34 story points (adjusted from backlog -- US-6.3, US-3.4, US-5.2 already done in Sprint 2)
**Status:** Ready for Developer Handoff

---

## Table of Contents

1. [Current State Summary](#1-current-state-summary)
2. [Feature Specifications](#2-feature-specifications)
3. [Database Migrations](#3-database-migrations)
4. [Implementation Order](#4-implementation-order)
5. [Staff Engineer Review (Gate 1)](#5-staff-engineer-review-gate-1)

---

## 1. Current State Summary

Sprint 2 delivered:
- CC/BCC, batch sending, attachments, API key rotation
- Open/click tracking via WebhookProcessor (pixel injection, link rewriting, `TrackingLink` table)
- SNS webhook processing (bounce, complaint, delivery event handling)
- Suppression management API (CRUD)
- Email logs API with advanced filtering (status, date range, tags, pagination, sorting)
- Template preview, search, soft-delete
- Remove domain endpoint
- List API keys endpoint
- Swagger/OpenAPI documentation
- CI/CD pipeline

**What exists for Dashboard:** A skeleton Blazor Server app at `src/EaaS.Dashboard/` with:
- `Program.cs` with Razor components and Serilog
- MudBlazor package referenced in csproj (but not initialized)
- Bare `Home.razor` placeholder page
- No MudBlazor layout, no sidebar, no HttpClient to API
- Auth env vars configured in docker-compose (`DASHBOARD_USERNAME`, `DASHBOARD_PASSWORD_HASH`)
- API accessible at `http://api:8080` from dashboard container

**What exists for Webhooks:** The `Webhook` entity exists in domain with `Id`, `TenantId`, `Url`, `Events[]`, `Secret`, `Status`, `CreatedAt`, `UpdatedAt`. DbSet `Webhooks` registered in `AppDbContext`. No API endpoints or dispatch logic yet.

**What exists for Analytics:** `EmailEvent` table has all event types (`Queued`, `Sent`, `Delivered`, `Bounced`, `Complained`, `Opened`, `Clicked`, `Failed`). Email entity has `Status`, `SentAt`, `DeliveredAt`, `OpenedAt`, `ClickedAt`. No analytics aggregation endpoints.

---

## 2. Feature Specifications

### 2.1 Analytics API (US-4.2) -- 3 pts

**New Endpoints:**

**A. `GET /api/v1/analytics/summary`**

Query parameters:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `date_from` | `datetime` | 30 days ago | Start of date range |
| `date_to` | `datetime` | now | End of date range |
| `domain` | `string` | null | Filter by sending domain |
| `api_key_id` | `guid` | null | Filter by API key |
| `template_id` | `guid` | null | Filter by template |

Response (200):
```json
{
  "success": true,
  "data": {
    "total_sent": 1250,
    "delivered": 1200,
    "bounced": 30,
    "complained": 5,
    "opened": 800,
    "clicked": 350,
    "failed": 15,
    "delivery_rate": 96.0,
    "open_rate": 66.67,
    "click_rate": 29.17,
    "bounce_rate": 2.4,
    "complaint_rate": 0.4
  }
}
```

Implementation:
- New feature folder: `Features/Analytics/`
- Files: `AnalyticsSummaryEndpoint.cs`, `AnalyticsSummaryQuery.cs`, `AnalyticsSummaryHandler.cs`
- Handler queries `emails` table with `COUNT` + `CASE WHEN` aggregation on `Status` column, filtered by tenant and date range.
- Open/click counts from `opened_at IS NOT NULL` / `clicked_at IS NOT NULL`.
- Rates calculated as percentages of `total_sent`.

**B. `GET /api/v1/analytics/timeline`**

Query parameters: same as summary, plus:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `granularity` | `string` | `day` | `hour` or `day` |

Response (200):
```json
{
  "success": true,
  "data": {
    "granularity": "day",
    "points": [
      {
        "timestamp": "2026-03-01T00:00:00Z",
        "sent": 45,
        "delivered": 43,
        "bounced": 1,
        "complained": 0,
        "opened": 30,
        "clicked": 12
      }
    ]
  }
}
```

Implementation:
- Files: `AnalyticsTimelineEndpoint.cs`, `AnalyticsTimelineQuery.cs`, `AnalyticsTimelineHandler.cs`
- Handler uses `GROUP BY DATE_TRUNC('{granularity}', created_at)` via raw SQL or EF Core `GroupBy`.
- Return sorted ascending by timestamp.
- For `hour` granularity, limit to 7-day max range (168 data points). For `day`, limit to 90-day max range.

**C. Register endpoints in `Program.cs`:**
```csharp
var analyticsGroup = app.MapGroup("/api/v1/analytics")
    .RequireAuthorization()
    .WithTags("Analytics");

AnalyticsSummaryEndpoint.Map(analyticsGroup);
AnalyticsTimelineEndpoint.Map(analyticsGroup);
```

---

### 2.2 Webhook CRUD (US-8.1 + US-8.3) -- 5 pts combined

**New Endpoints:**

**A. `POST /api/v1/webhooks`** -- Create webhook

Request:
```json
{
  "url": "https://example.com/hooks/email",
  "events": ["delivered", "bounced", "complained", "opened", "clicked"],
  "secret": "whsec_optional_user_provided_secret"
}
```

Response (201):
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "url": "https://example.com/hooks/email",
    "events": ["delivered", "bounced", "complained", "opened", "clicked"],
    "status": "active",
    "created_at": "2026-03-27T10:00:00Z"
  }
}
```

Validation:
- `url`: required, must be HTTPS, max 2048 chars.
- `events`: required, non-empty, values must be from: `sent`, `delivered`, `bounced`, `complained`, `opened`, `clicked`, `failed`.
- `secret`: optional. If not provided, system generates one (HMAC signing key). Either way, the secret is returned once at creation.
- Max 10 webhook endpoints per tenant.
- On create, send a test `ping` POST to verify reachability (timeout 5s). If unreachable, return 422 with message.

**B. `GET /api/v1/webhooks`** -- List webhooks

Response (200): Paginated list of webhook configs (secret NOT included in list).

**C. `GET /api/v1/webhooks/{id}`** -- Get webhook detail

Response (200): Single webhook detail (secret NOT included).

**D. `PUT /api/v1/webhooks/{id}`** -- Update webhook

Request: same shape as create. Partial update allowed.

**E. `DELETE /api/v1/webhooks/{id}`** -- Delete webhook

Response (200): `{ "success": true, "data": { "message": "Webhook deleted" } }`

Implementation:
- New feature folder: `Features/Webhooks/`
- Files: `CreateWebhookEndpoint.cs`, `CreateWebhookCommand.cs`, `CreateWebhookHandler.cs`, `CreateWebhookValidator.cs`, `ListWebhooksEndpoint.cs`, `ListWebhooksQuery.cs`, `ListWebhooksHandler.cs`, `GetWebhookEndpoint.cs`, `GetWebhookQuery.cs`, `GetWebhookHandler.cs`, `UpdateWebhookEndpoint.cs`, `UpdateWebhookCommand.cs`, `UpdateWebhookHandler.cs`, `UpdateWebhookValidator.cs`, `DeleteWebhookEndpoint.cs`, `DeleteWebhookCommand.cs`, `DeleteWebhookHandler.cs`
- Register endpoint group in `Program.cs`:
  ```csharp
  var webhooksGroup = app.MapGroup("/api/v1/webhooks")
      .RequireAuthorization()
      .WithTags("Webhooks");
  ```

---

### 2.3 Webhook Dispatch (US-8.2) -- 5 pts

**Architecture:**
- Add a new MassTransit consumer `WebhookDispatchConsumer` in the **Worker** project.
- When an email event occurs (delivery, bounce, complaint, open, click), publish a `WebhookDispatchMessage` to a new `webhook-dispatch` queue.
- The consumer queries active webhooks for the tenant that subscribe to that event type, then fires HTTP POST callbacks.

**Message Contract:**
```csharp
public record WebhookDispatchMessage
{
    public Guid TenantId { get; init; }
    public string EventType { get; init; } = string.Empty;  // "delivered", "bounced", etc.
    public Guid EmailId { get; init; }
    public string MessageId { get; init; } = string.Empty;
    public string Data { get; init; } = "{}";  // Event-specific JSON payload
    public DateTime Timestamp { get; init; }
}
```

**Webhook Payload (what the user's endpoint receives):**
```json
{
  "event": "delivered",
  "message_id": "eaas_abc123",
  "email_id": "guid",
  "timestamp": "2026-03-27T10:30:00Z",
  "data": {
    "recipient": "user@example.com",
    "subject": "Your order confirmation"
  }
}
```

**HMAC Signature:**
- Compute `HMAC-SHA256(webhook.Secret, raw_json_body)`.
- Send as `X-EaaS-Signature` header: `sha256={hex_digest}`.
- Include `X-EaaS-Event` header with event type.
- Include `X-EaaS-Delivery-Id` header with unique delivery attempt ID.

**Retry Policy:**
- 5 retries with exponential backoff: 10s, 30s, 90s, 270s, 810s.
- Use MassTransit retry + redelivery for this (configure on the consumer).
- After 5 failures, log the failure. Do NOT disable the webhook -- just record the failure.

**Event Dispatch Points (where to publish `WebhookDispatchMessage`):**
- In `DeliveryHandler`, `BounceHandler`, `ComplaintHandler` (WebhookProcessor): after processing the SNS event, publish `WebhookDispatchMessage` to RabbitMQ.
- In the open/click tracking endpoints (WebhookProcessor): after recording the event, publish `WebhookDispatchMessage`.
- This requires adding MassTransit publisher to WebhookProcessor (it currently only does HTTP, no RabbitMQ).

**WebhookProcessor Changes:**
- Add MassTransit + RabbitMQ to WebhookProcessor's DI so it can publish messages.
- Add RabbitMQ env vars to webhook-processor in docker-compose.yml.

**Worker Changes:**
- Register `WebhookDispatchConsumer`.
- Add `IHttpClientFactory` for making webhook HTTP calls.

**New Entity: `WebhookDeliveryLog`** (for observability):
```csharp
public class WebhookDeliveryLog
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public Guid EmailId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

### 2.4 Dashboard Bootstrap (Foundation for all UI stories)

Before building any pages, the dashboard needs foundational setup:

**A. MudBlazor Initialization**

Update `Program.cs`:
```csharp
builder.Services.AddMudServices();
```

Update `App.razor` -- add MudBlazor CSS/JS in `<head>`:
```html
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
```
Before closing `</body>`:
```html
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

Update `_Imports.razor`:
```razor
@using MudBlazor
```

**B. HttpClient to API**

Register in `Program.cs`:
```csharp
builder.Services.AddHttpClient("EaaSApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://api:8080");
});
```

Create `Services/ApiClient.cs` -- a typed wrapper around `HttpClient` that calls the EaaS API endpoints. Methods:
- `GetAnalyticsSummaryAsync(DateOnly from, DateOnly to)` -> summary DTO
- `GetAnalyticsTimelineAsync(DateOnly from, DateOnly to, string granularity)` -> timeline DTO
- `GetEmailsAsync(EmailFilterDto filters)` -> paginated emails
- `GetEmailAsync(string messageId)` -> single email
- `GetTemplatesAsync(string? search, int page, int pageSize)` -> paginated templates
- `GetTemplateAsync(Guid id)` -> single template
- `CreateTemplateAsync(CreateTemplateDto dto)` -> template
- `UpdateTemplateAsync(Guid id, UpdateTemplateDto dto)` -> template
- `DeleteTemplateAsync(Guid id)` -> void
- `PreviewTemplateAsync(Guid id, Dictionary<string,string> variables)` -> preview DTO
- `GetDomainsAsync()` -> paginated domains
- `VerifyDomainAsync(Guid id)` -> domain
- `GetSuppressionsAsync(string? search, string? reason, int page, int pageSize)` -> paginated suppressions
- `AddSuppressionAsync(string email, string reason)` -> suppression
- `RemoveSuppressionAsync(Guid id)` -> void
- `GetWebhooksAsync()` -> paginated webhooks

All methods deserialize the standard `{ success, data }` envelope.

**C. Simple Auth Middleware**

The dashboard uses cookie auth with a single admin password:
- Login page at `/login`.
- POST form with password.
- Compare BCrypt hash against `DASHBOARD_PASSWORD_HASH` env var.
- Set auth cookie on success.
- Protect all other pages with `[Authorize]`.

Implementation:
- Add cookie authentication in `Program.cs`.
- Create `Components/Pages/Login.razor` with a MudBlazor form.
- Add `AuthorizeView` wrapper in `MainLayout.razor`.

**D. Sidebar Layout**

Replace `MainLayout.razor` with MudBlazor layout:
```razor
@inherits LayoutComponentBase

<MudThemeProvider />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit"
                       Edge="Edge.Start" OnClick="ToggleDrawer" />
        <MudText Typo="Typo.h6" Class="ml-3">EaaS Dashboard</MudText>
        <MudSpacer />
        <MudIconButton Icon="@Icons.Material.Filled.Logout" Color="Color.Inherit"
                       Href="/logout" />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" ClipMode="DrawerClipMode.Always" Elevation="2">
        <NavMenu />
    </MudDrawer>

    <MudMainContent Class="pa-4">
        @Body
    </MudMainContent>
</MudLayout>

@code {
    bool _drawerOpen = true;
    void ToggleDrawer() => _drawerOpen = !_drawerOpen;
}
```

Create `Components/Layout/NavMenu.razor`:
```razor
<MudNavMenu>
    <MudNavLink Href="/" Match="NavLinkMatch.All"
                Icon="@Icons.Material.Filled.Dashboard">Overview</MudNavLink>
    <MudNavLink Href="/emails" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Email">Email Logs</MudNavLink>
    <MudNavLink Href="/templates" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Description">Templates</MudNavLink>
    <MudNavLink Href="/domains" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Dns">Domains</MudNavLink>
    <MudNavLink Href="/analytics" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Analytics">Analytics</MudNavLink>
    <MudNavLink Href="/suppressions" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Block">Suppressions</MudNavLink>
</MudNavMenu>
```

---

### 2.5 Dashboard Overview Page (US-7.1) -- 5 pts

**Route:** `/`

**Layout:**
- Row 1: 6 stat cards (Total Sent, Delivered, Bounced, Complained, Opened, Clicked) -- each with count + rate + trend arrow.
- Row 2: Line chart (daily send volume, last 30 days) + Pie chart (delivery status breakdown).
- Row 3: Recent emails table (last 10) + System health panel.

**Data Sources:**
- `GET /api/v1/analytics/summary?date_from={30d ago}` for stat cards.
- `GET /api/v1/analytics/timeline?granularity=day&date_from={30d ago}` for line chart.
- `GET /api/v1/emails?page_size=10&sort_by=created_at&sort_dir=desc` for recent emails.
- `GET /health` from API for system health (direct HTTP call).

**Components:**
- `Components/Pages/Home.razor` -- the page.
- `Components/Dashboard/StatCard.razor` -- reusable stat card (title, value, rate, icon, color).
- Use `MudChart` (built into MudBlazor) for line and pie charts.
- Use `MudSimpleTable` for recent emails.
- Use `MudAlert` for system health status.

**Status Badges:** Use `MudChip` with colors:
- `Delivered` = green, `Bounced` = red, `Complained` = orange, `Queued` = blue, `Failed` = red dark.

---

### 2.6 Email Log Viewer (US-7.2) -- 5 pts

**Route:** `/emails`

**Layout:**
- Filter bar (top): Status dropdown, date range picker, search text field, template dropdown, "Apply" button.
- Data table: `MudDataGrid<EmailDto>` with server-side pagination, sorting.
- Columns: Status (chip), To, Subject, Template, Created At, Sent At.
- Click row -> detail dialog (`MudDialog`).

**Detail Dialog Contents:**
- Full email metadata (from, to, cc, bcc, subject, template, batch, tags).
- Status timeline (events table: queued -> sent -> delivered -> opened).
- HTML body preview (rendered in an iframe or `MudMarkup`).

**Data Source:** `GET /api/v1/emails?status={}&date_from={}&date_to={}&to={}&page={}&page_size=50&sort_by={}&sort_dir={}`

**Implementation:**
- `Components/Pages/Emails.razor` -- page with filter bar and grid.
- `Components/Emails/EmailDetailDialog.razor` -- MudDialog showing full detail.
- `Components/Emails/EmailStatusChip.razor` -- reusable status badge.
- Server-side pagination: on page change / filter change, call API with updated params.

---

### 2.7 Template Manager UI (US-7.3) -- 5 pts

**Route:** `/templates`

**Layout:**
- Template list (left/main): `MudDataGrid` with Name, Version, Updated At, actions.
- Actions per row: Edit, Preview, Delete.
- "New Template" button -> create dialog.
- Edit/Create dialog: `MudDialog` with form fields (name, subject template, HTML body textarea, text body textarea, variables schema).
- Preview panel: after saving, "Preview" button opens a dialog with variable inputs + rendered output.

**Data Sources:**
- `GET /api/v1/templates?search={}&page={}&page_size=20`
- `POST /api/v1/templates` (create)
- `PUT /api/v1/templates/{id}` (update)
- `DELETE /api/v1/templates/{id}` (soft delete)
- `POST /api/v1/templates/{id}/preview` (preview)

**Implementation:**
- `Components/Pages/Templates.razor` -- list page.
- `Components/Templates/TemplateFormDialog.razor` -- create/edit MudDialog with form.
- `Components/Templates/TemplatePreviewDialog.razor` -- preview with variable inputs + rendered HTML display.
- For HTML body input, use a `MudTextField` with `Lines="12"` (multi-line). Full code editor (Monaco/CodeMirror) is a Sprint 4 enhancement -- not worth the JS interop complexity now.

---

### 2.8 Domain Manager UI (US-7.4) -- 3 pts

**Route:** `/domains`

**Layout:**
- Domain list: `MudDataGrid` with Domain Name, Status (chip), Verified At, actions.
- Status chips: `Verified` = green, `PendingVerification` = yellow, `Failed` = red.
- Click row -> expand to show DNS records table (`MudSimpleTable`) with Type, Name, Value, copy button.
- "Verify Now" button per domain -> calls verify endpoint, shows result.
- "Add Domain" button -> dialog with domain name input.

**Data Sources:**
- `GET /api/v1/domains`
- `POST /api/v1/domains` (add)
- `POST /api/v1/domains/{id}/verify` (verify)

**Implementation:**
- `Components/Pages/Domains.razor` -- list page with expandable rows.
- `Components/Domains/AddDomainDialog.razor` -- MudDialog with domain name input.
- `Components/Domains/DnsRecordTable.razor` -- table of DNS records with copy-to-clipboard.

---

### 2.9 Analytics Dashboard (US-7.5) -- 5 pts

**Route:** `/analytics`

**Layout:**
- Date range selector (top bar): `MudDateRangePicker` with presets (7d, 30d, 90d).
- Row 1: Same 6 stat cards as overview (but for selected date range).
- Row 2: Time-series line chart (sent, delivered, bounced over time) -- `MudChart` with `ChartType.Line`.
- Row 3: Donut chart (delivery status breakdown) + Bar chart (top 5 templates by send count).

**Data Sources:**
- `GET /api/v1/analytics/summary?date_from={}&date_to={}`
- `GET /api/v1/analytics/timeline?date_from={}&date_to={}&granularity=day`

**Implementation:**
- `Components/Pages/Analytics.razor` -- full page.
- Reuse `StatCard.razor` component from overview.
- Use `MudChart` (line + donut). MudBlazor charts are sufficient for this scope.
- Date range change triggers re-fetch of both endpoints.

---

### 2.10 Suppression List Manager UI (US-7.6) -- 3 pts

**Route:** `/suppressions`

**Layout:**
- Search bar + reason filter dropdown + "Add Suppression" button.
- `MudDataGrid` with Email, Reason (chip), Source Message, Suppressed At, Delete action.
- Reason chips: `HardBounce` = red, `Complaint` = orange, `Manual` = blue.
- "Add Suppression" -> dialog with email input.
- Delete -> confirmation dialog -> calls remove endpoint.

**Data Sources:**
- `GET /api/v1/suppressions?search={}&reason={}&page={}&page_size=50`
- `POST /api/v1/suppressions` (add)
- `DELETE /api/v1/suppressions/{id}` (remove)

**Implementation:**
- `Components/Pages/Suppressions.razor` -- list page.
- `Components/Suppressions/AddSuppressionDialog.razor` -- MudDialog with email + reason inputs.

---

## 3. Database Migrations

### 3.1 Required Schema Changes

```sql
-- ============================================================
-- EaaS Sprint 3 Migration
-- ============================================================

-- -----------------------------------------------------------
-- 1. Webhook delivery log table (for US-8.2 observability)
-- -----------------------------------------------------------
CREATE TABLE webhook_delivery_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    webhook_id UUID NOT NULL REFERENCES webhooks(id) ON DELETE CASCADE,
    email_id UUID NOT NULL REFERENCES emails(id),
    event_type VARCHAR(50) NOT NULL,
    status_code INT NOT NULL DEFAULT 0,
    success BOOLEAN NOT NULL DEFAULT false,
    error_message TEXT,
    attempt_number INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_webhook_delivery_logs_webhook
    ON webhook_delivery_logs(webhook_id, created_at DESC);

CREATE INDEX idx_webhook_delivery_logs_email
    ON webhook_delivery_logs(email_id);

-- -----------------------------------------------------------
-- 2. Index for analytics aggregation (date range queries)
-- -----------------------------------------------------------
CREATE INDEX idx_emails_analytics
    ON emails(tenant_id, created_at, status)
    WHERE status != 'queued';
```

### 3.2 EF Core Entity Changes

**New Entity:** `WebhookDeliveryLog` (see section 2.3).

**Entity Configuration:**
- Add `WebhookDeliveryLog` to `AppDbContext` as `DbSet<WebhookDeliveryLog>`.
- Configure table name `webhook_delivery_logs`, column mappings in a configuration class.

**Migration:**
```bash
dotnet ef migrations add Sprint3_WebhookDeliveryLogs --project src/EaaS.Infrastructure --startup-project src/EaaS.Api
```

---

## 4. Implementation Order

| # | Feature | Story IDs | Est. Hours | Dependencies | Rationale |
|---|---------|-----------|------------|--------------|-----------|
| 1 | **Database Migration** | -- | 0.5h | None | Add webhook_delivery_logs table + analytics index |
| 2 | **Analytics API** | US-4.2 | 2h | Migration (#1) | Dashboard charts depend on this. Backend-only, well-defined scope. |
| 3 | **Webhook CRUD** | US-8.1, US-8.3 | 2.5h | None | Standard CRUD endpoints, straightforward. Webhook entity already exists. |
| 4 | **Webhook Dispatch** | US-8.2 | 4h | Webhook CRUD (#3), Migration (#1) | Most complex backend task. Touches WebhookProcessor + Worker. |
| 5 | **Dashboard Bootstrap** | -- | 2h | None | MudBlazor init, layout, nav, HttpClient, auth. Foundation for all 6 pages. |
| 6 | **Dashboard Overview** | US-7.1 | 2.5h | Analytics API (#2), Bootstrap (#5) | First real page. Establishes component patterns (StatCard, charts). |
| 7 | **Email Log Viewer** | US-7.2 | 3h | Bootstrap (#5) | Highest-value dashboard page. Server-side grid + detail dialog. |
| 8 | **Suppression Manager UI** | US-7.6 | 1.5h | Bootstrap (#5) | Simple CRUD page. Uses patterns from email log viewer. |
| 9 | **Domain Manager UI** | US-7.4 | 2h | Bootstrap (#5) | Expandable rows + DNS record display. Medium complexity. |
| 10 | **Template Manager UI** | US-7.3 | 3h | Bootstrap (#5) | Most complex UI page (form dialogs, preview). |
| 11 | **Analytics Dashboard** | US-7.5 | 2.5h | Analytics API (#2), Bootstrap (#5) | Charts page. Reuses StatCard from overview. |

**Total estimated: ~25.5 hours of development time**

### Critical Path

```
Migration (#1) --> Analytics API (#2) --> Dashboard Overview (#6) --> Analytics Dashboard (#11)
                                     \
Dashboard Bootstrap (#5) -----------> Email Log Viewer (#7) --> Suppression (#8) --> Domain (#9) --> Template (#10)

Webhook CRUD (#3) --> Webhook Dispatch (#4)
```

Three parallel chains:
1. **Analytics chain:** Migration -> Analytics API -> Overview page -> Analytics page
2. **Dashboard pages chain:** Bootstrap -> Email Logs -> Suppressions -> Domains -> Templates
3. **Webhooks chain:** CRUD -> Dispatch (independent of dashboard)

### Priority Tiers

**Tier 1 -- Must Ship (core dashboard value):**
Items 1-2, 5-7 (migration, analytics API, bootstrap, overview, email logs)

**Tier 2 -- High Value:**
Items 3-4, 8-11 (webhooks, remaining dashboard pages)

**Tier 3 -- Can Defer to Sprint 4:**
- Code editor for templates (Monaco/CodeMirror JS interop)
- Template version history diff view
- CSV export for suppressions
- Webhook delivery log viewer in dashboard

---

## 5. Staff Engineer Review (Gate 1)

### 5.1 Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| **MudBlazor charts are limited** | Medium | MudBlazor's built-in `MudChart` supports line, bar, pie, donut. It lacks interactive tooltips and zoom. Acceptable for Sprint 3. If charts need more interactivity, add Chart.js via JS interop in Sprint 4. Do NOT add Chart.js now -- it doubles the charting complexity. |
| **Dashboard-to-API auth gap** | Medium | Dashboard calls API internally without an API key. The API requires `Authorization: Bearer` header. **REQUIRED FIX:** Either (a) create a system-level API key for the dashboard at startup and inject it into HttpClient, or (b) add an internal-only bypass that trusts requests from the docker network. Option (a) is cleaner -- use a seeded "dashboard" API key with full read access. Store the key hash in config. |
| **WebhookProcessor needs RabbitMQ** | Low | WebhookProcessor currently has no MassTransit dependency. Adding it is straightforward but increases its memory footprint. The 128MB container limit should be bumped to 192MB. |
| **Analytics query performance** | Low | Aggregating over the entire emails table with `GROUP BY` could be slow at scale. For Sprint 3 volumes (<10K emails), direct queries are fine. Add a materialized view or pre-computed daily aggregates in Sprint 4 if needed. The `idx_emails_analytics` index covers the hot path. |
| **Webhook retry storms** | Low | If a user's endpoint is permanently down, 5 retries x N events could create a retry backlog. MassTransit's built-in retry with exponential backoff handles this well. After final failure, the message goes to the error queue, not re-retried. |

### 5.2 Architecture Concerns

1. **Dashboard-API communication pattern:** The dashboard calls the API over HTTP, not directly to the database. This is correct -- keeps the dashboard as a pure UI layer. However, for the health check panel, the dashboard should call the API's `/health` endpoint rather than independently checking postgres/redis. Single source of truth.

2. **Webhook dispatch decoupling:** Publishing `WebhookDispatchMessage` from WebhookProcessor to Worker via RabbitMQ is the right pattern. It keeps the webhook HTTP dispatch logic in the Worker (which already has retry infrastructure) rather than bloating the WebhookProcessor. The WebhookProcessor stays focused on inbound SNS + tracking.

3. **Template editor simplification:** The spec correctly defers Monaco/CodeMirror to Sprint 4. A multi-line `MudTextField` is sufficient for editing HTML/Liquid templates. The preview dialog gives immediate feedback. This avoids 4+ hours of JS interop setup.

4. **No real-time updates in Sprint 3:** The dashboard pages fetch data on load and on user action (filter, page change). No SignalR push updates. This is acceptable for Sprint 3. Real-time updates (new email arrives, status changes) can be added in Sprint 4 with SignalR + MudBlazor's `StateHasChanged`.

### 5.3 Required Changes (incorporated above)

1. **Dashboard API key:** Add a seeded system API key for the dashboard's HttpClient (see Risk table). The `ApiClient` service must include `Authorization: Bearer {dashboard_api_key}` on all requests.
2. **WebhookProcessor memory:** Bump container limit from 128MB to 192MB in docker-compose.
3. **Analytics rate limits:** The analytics endpoints should have their own rate limit (10 requests/minute) since they run aggregation queries. Add to rate limiting middleware.
4. **Webhook delivery log retention:** Add a note that webhook delivery logs should be pruned after 30 days. Implement via the existing cleanup job pattern.

### 5.4 Approval

**Gate 1 PASSED** with the 4 required changes above incorporated into the spec.

The scope is achievable in a 24-hour sprint because:
- 6 dashboard pages are UI-only (calling existing API endpoints via HttpClient).
- Analytics API is 2 straightforward aggregation endpoints.
- Webhook CRUD is standard -- entity already exists.
- Webhook dispatch is the only complex piece, and MassTransit handles the heavy lifting (retry, DLQ).
- MudBlazor provides pre-built components (data grid, charts, dialogs, chips) that eliminate custom CSS/JS work.
