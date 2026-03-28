# EaaS - Sprint 3 Technical Specification

**Version:** 2.0
**Date:** 2026-03-27
**Author:** Senior Architect
**Reviewer:** Staff Engineer (Gate 1)
**Sprint:** 3
**Scope:** 10 stories, 34 story points (adjusted from backlog -- US-6.3, US-3.4, US-5.2 already done in Sprint 2)
**Status:** Ready for Developer Handoff

> **Dashboard Technology:** Next.js 15 + shadcn/ui + Tailwind CSS. Backend specs (Analytics API, Webhooks) unchanged.

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

**What exists for Dashboard:** A new Next.js 15 application at `dashboard/` (the old .NET skeleton has been removed). Current state:
- Fresh Next.js 15 project with shadcn/ui and Tailwind CSS
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

Before building any pages, the dashboard needs foundational setup. The Next.js application lives at `dashboard/`.

**A. Project Structure**

```
dashboard/
├── src/
│   ├── app/                    # Next.js App Router
│   │   ├── layout.tsx          # Root layout with sidebar
│   │   ├── page.tsx            # Overview dashboard (/)
│   │   ├── login/page.tsx      # Login page
│   │   ├── emails/
│   │   │   ├── page.tsx        # Email log viewer
│   │   │   └── [id]/page.tsx   # Email detail
│   │   ├── templates/
│   │   │   ├── page.tsx        # Template manager
│   │   │   └── [id]/page.tsx   # Template editor
│   │   ├── domains/page.tsx    # Domain manager
│   │   ├── analytics/page.tsx  # Analytics dashboard
│   │   └── suppressions/page.tsx # Suppression manager
│   ├── components/
│   │   ├── ui/                 # shadcn/ui components (auto-generated)
│   │   ├── layout/             # Sidebar, AppShell, TopBar
│   │   ├── emails/             # Email-specific components
│   │   ├── templates/          # Template-specific components
│   │   ├── domains/            # Domain-specific components
│   │   ├── analytics/          # Chart components
│   │   └── suppressions/       # Suppression-specific components
│   ├── lib/
│   │   ├── api.ts              # API client (typed fetch wrapper)
│   │   ├── auth.ts             # Auth context/hooks
│   │   ├── query-keys.ts       # React Query key factory
│   │   └── utils.ts            # Utilities (cn helper, formatters)
│   └── types/
│       └── index.ts            # TypeScript interfaces matching API DTOs
├── public/
├── tailwind.config.ts
├── next.config.ts
├── components.json             # shadcn/ui config
├── package.json
├── tsconfig.json
├── Dockerfile
└── .env.local
```

**B. Tech Stack**

| Technology | Version | Purpose |
|------------|---------|---------|
| Next.js | 15 | App Router, Server Components where possible |
| TypeScript | 5.x | Strict mode enabled |
| Tailwind CSS | 4 | Utility-first styling |
| shadcn/ui | latest | UI component library (Radix UI primitives) |
| Recharts | 2.x | Analytics charts (line, bar, pie, area) |
| TanStack Query | 5.x | Data fetching, caching, background refetch |
| Lucide React | latest | Icon library |
| next-themes | latest | Light/dark mode support |

**C. shadcn/ui Components to Install**

```bash
npx shadcn@latest init
npx shadcn@latest add button card dialog drawer dropdown-menu input label \
  select table tabs badge toast skeleton sheet separator scroll-area \
  popover command calendar chart avatar alert tooltip
```

**D. API Client (`src/lib/api.ts`)**

Typed fetch wrapper with error handling:

```typescript
const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: string;
}

class ApiClient {
  private apiKey: string | null = null;

  setApiKey(key: string) { this.apiKey = key; }

  async get<T>(path: string, params?: Record<string, string>): Promise<T> {
    const url = new URL(`${API_BASE}${path}`);
    if (params) Object.entries(params).forEach(([k, v]) => url.searchParams.set(k, v));

    const res = await fetch(url.toString(), {
      headers: this.headers(),
      credentials: "include",
    });

    if (!res.ok) throw new ApiError(res.status, await res.text());
    const json: ApiResponse<T> = await res.json();
    if (!json.success) throw new ApiError(400, json.error ?? "Unknown error");
    return json.data;
  }

  async post<T>(path: string, body?: unknown): Promise<T> { /* similar */ }
  async put<T>(path: string, body?: unknown): Promise<T> { /* similar */ }
  async delete<T>(path: string): Promise<T> { /* similar */ }

  private headers(): HeadersInit {
    const h: HeadersInit = { "Content-Type": "application/json" };
    if (this.apiKey) h["Authorization"] = `Bearer ${this.apiKey}`;
    return h;
  }
}

export const api = new ApiClient();
```

Methods (via TanStack Query hooks in each page):
- `getAnalyticsSummary(dateFrom, dateTo)` -> summary DTO
- `getAnalyticsTimeline(dateFrom, dateTo, granularity)` -> timeline DTO
- `getEmails(filters)` -> paginated emails
- `getEmail(messageId)` -> single email
- `getTemplates(search, page, pageSize)` -> paginated templates
- `getTemplate(id)` -> single template
- `createTemplate(dto)` -> template
- `updateTemplate(id, dto)` -> template
- `deleteTemplate(id)` -> void
- `previewTemplate(id, variables)` -> preview DTO
- `getDomains()` -> paginated domains
- `verifyDomain(id)` -> domain
- `getSuppressions(search, reason, page, pageSize)` -> paginated suppressions
- `addSuppression(email, reason)` -> suppression
- `removeSuppression(id)` -> void
- `getWebhooks()` -> paginated webhooks

All methods deserialize the standard `{ success, data }` envelope.

**E. TanStack Query Setup**

```typescript
// src/app/providers.tsx
"use client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,       // 30s before refetch
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}
```

**F. Authentication**

Simple cookie-based auth with a single admin password:

- Login page at `/login` -- client component with email/password form using shadcn Input + Button.
- POST to `/api/auth/login` (Next.js API route) with password.
- API route compares BCrypt hash against `DASHBOARD_PASSWORD_HASH` env var.
- On success, set an HTTP-only secure cookie with a signed JWT (or simple session token).
- Middleware (`src/middleware.ts`) checks the cookie on all routes except `/login`.
- Redirect to `/login` if unauthenticated.

```typescript
// src/middleware.ts
import { NextRequest, NextResponse } from "next/server";

export function middleware(request: NextRequest) {
  const token = request.cookies.get("eaas_session")?.value;
  if (!token && !request.nextUrl.pathname.startsWith("/login")) {
    return NextResponse.redirect(new URL("/login", request.url));
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
```

**G. App Shell Layout (`src/app/layout.tsx`)**

Root layout with collapsible sidebar navigation + top bar:

```tsx
// src/app/layout.tsx
import { Providers } from "./providers";
import { AppShell } from "@/components/layout/app-shell";

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <Providers>
          <AppShell>{children}</AppShell>
        </Providers>
      </body>
    </html>
  );
}
```

**Sidebar Navigation (`src/components/layout/sidebar.tsx`):**

```tsx
const navItems = [
  { href: "/", label: "Overview", icon: LayoutDashboard },
  { href: "/emails", label: "Email Logs", icon: Mail },
  { href: "/templates", label: "Templates", icon: FileText },
  { href: "/domains", label: "Domains", icon: Globe },
  { href: "/analytics", label: "Analytics", icon: BarChart3 },
  { href: "/suppressions", label: "Suppressions", icon: ShieldBan },
];
```

Uses shadcn `Sheet` for mobile (responsive drawer) and a fixed sidebar on desktop. Active route highlighted via `usePathname()`.

**H. Docker Integration**

Dockerfile (`dashboard/Dockerfile`) -- multi-stage build:

```dockerfile
# Stage 1: Install dependencies
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --production=false

# Stage 2: Build
FROM node:22-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1
RUN npm run build

# Stage 3: Production runtime
FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

RUN addgroup --system --gid 1001 nodejs && \
    adduser --system --uid 1001 nextjs

COPY --from=builder /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs
EXPOSE 3000
ENV PORT=3000
CMD ["node", "server.js"]
```

`next.config.ts` must include `output: "standalone"` for the Docker build.

**I. Cleanup: Completed**

- The old .NET dashboard project has been removed from the solution.
- No legacy dashboard packages remain in `Directory.Packages.props`.

---

### 2.5 Dashboard Overview Page (US-7.1) -- 5 pts

**Route:** `/`

**Layout:**
- Row 1: 6 stat cards (Total Sent, Delivered, Bounced, Complained, Opened, Clicked) -- each with count + rate + trend icon.
- Row 2: Line chart (daily send volume, last 30 days) + Pie chart (delivery status breakdown).
- Row 3: Recent emails table (last 10) + System health panel.

**Data Sources:**
- `GET /api/v1/analytics/summary?date_from={30d ago}` for stat cards.
- `GET /api/v1/analytics/timeline?granularity=day&date_from={30d ago}` for line chart.
- `GET /api/v1/emails?page_size=10&sort_by=created_at&sort_dir=desc` for recent emails.
- `GET /health` from API for system health (direct HTTP call).

**Components:**
- `src/app/page.tsx` -- the overview page (server component shell, client data islands).
- `src/components/analytics/stat-card.tsx` -- reusable stat card using shadcn `Card`. Props: `title`, `value`, `rate`, `icon`, `trend` ("up" | "down" | "flat"), `color`.
- `src/components/analytics/send-volume-chart.tsx` -- Recharts `LineChart` / `AreaChart` for time-series.
- `src/components/analytics/status-breakdown-chart.tsx` -- Recharts `PieChart` for delivery status.
- `src/components/emails/recent-emails-table.tsx` -- shadcn `Table` for last 10 emails.
- `src/components/layout/health-panel.tsx` -- shadcn `Alert` for system health status.

**Status Badges:** Use shadcn `Badge` with variants:
- `Delivered` = green (`bg-green-100 text-green-800`), `Bounced` = red, `Complained` = orange, `Queued` = blue, `Failed` = red dark.

---

### 2.6 Email Log Viewer (US-7.2) -- 5 pts

**Route:** `/emails`

**Layout:**
- Filter bar (top): Status select (shadcn `Select`), date range picker (shadcn `Calendar` + `Popover`), search input (shadcn `Input`), template select, "Apply" button.
- Data table: shadcn `Table` with custom server-side pagination, sorting. Use TanStack Query with `keepPreviousData: true` for smooth page transitions.
- Columns: Status (Badge), To, Subject, Template, Created At, Sent At.
- Click row -> opens detail drawer (shadcn `Sheet` side panel).

**Detail Drawer Contents:**
- Full email metadata (from, to, cc, bcc, subject, template, batch, tags).
- Status timeline (event list: queued -> sent -> delivered -> opened) with timestamps.
- HTML body preview (rendered in a sandboxed `<iframe srcDoc={html} />`).

**Data Source:** `GET /api/v1/emails?status={}&date_from={}&date_to={}&to={}&page={}&page_size=50&sort_by={}&sort_dir={}`

**Implementation:**
- `src/app/emails/page.tsx` -- page with filter bar and table.
- `src/components/emails/email-filter-bar.tsx` -- filter controls row.
- `src/components/emails/email-table.tsx` -- data table with pagination.
- `src/components/emails/email-detail-sheet.tsx` -- shadcn Sheet (side drawer) showing full detail.
- `src/components/emails/email-status-badge.tsx` -- reusable status Badge.
- Server-side pagination: on page change / filter change, update URL search params and refetch via TanStack Query.

---

### 2.7 Template Manager UI (US-7.3) -- 5 pts

**Route:** `/templates`

**Layout:**
- Template list (main): shadcn `Table` with Name, Version, Updated At, actions column.
- Actions per row: Edit, Preview, Delete (via shadcn `DropdownMenu`).
- "New Template" button -> create dialog (shadcn `Dialog`).
- Edit/Create dialog: shadcn `Dialog` with form fields (shadcn `Input` for name, `Input` for subject template, `Textarea` for HTML body, `Textarea` for text body, JSON editor for variables schema).
- Preview panel: after saving, "Preview" button opens a dialog with variable inputs + rendered HTML output in an iframe.

**Data Sources:**
- `GET /api/v1/templates?search={}&page={}&page_size=20`
- `POST /api/v1/templates` (create)
- `PUT /api/v1/templates/{id}` (update)
- `DELETE /api/v1/templates/{id}` (soft delete)
- `POST /api/v1/templates/{id}/preview` (preview)

**Implementation:**
- `src/app/templates/page.tsx` -- list page.
- `src/components/templates/template-form-dialog.tsx` -- create/edit Dialog with form.
- `src/components/templates/template-preview-dialog.tsx` -- preview with variable inputs + rendered HTML display.
- For HTML body input, use a shadcn `Textarea` with monospace font and generous height. Full code editor (Monaco/CodeMirror) is a Sprint 4 enhancement -- not worth the bundle size now.

---

### 2.8 Domain Manager UI (US-7.4) -- 3 pts

**Route:** `/domains`

**Layout:**
- Domain list: shadcn `Table` with Domain Name, Status (Badge), Verified At, actions.
- Status badges: `Verified` = green, `PendingVerification` = yellow, `Failed` = red.
- Click row -> expand to show DNS records (collapsible row using shadcn `Collapsible` or inline expand) with Type, Name, Value, copy button (shadcn `Button` with copy icon).
- "Verify Now" button per domain -> calls verify endpoint, shows toast result.
- "Add Domain" button -> Dialog with domain name input.

**Data Sources:**
- `GET /api/v1/domains`
- `POST /api/v1/domains` (add)
- `POST /api/v1/domains/{id}/verify` (verify)

**Implementation:**
- `src/app/domains/page.tsx` -- list page with expandable rows.
- `src/components/domains/add-domain-dialog.tsx` -- Dialog with domain name input.
- `src/components/domains/dns-record-table.tsx` -- table of DNS records with copy-to-clipboard.

---

### 2.9 Analytics Dashboard (US-7.5) -- 5 pts

**Route:** `/analytics`

**Layout:**
- Date range selector (top bar): shadcn `Calendar` inside a `Popover` with preset buttons (7d, 30d, 90d).
- Row 1: Same 6 stat cards as overview (but for selected date range).
- Row 2: Time-series line chart (sent, delivered, bounced over time) -- Recharts `LineChart` with tooltips and legend.
- Row 3: Donut chart (delivery status breakdown, Recharts `PieChart`) + Bar chart (top 5 templates by send count, Recharts `BarChart`).

**Data Sources:**
- `GET /api/v1/analytics/summary?date_from={}&date_to={}`
- `GET /api/v1/analytics/timeline?date_from={}&date_to={}&granularity=day`

**Implementation:**
- `src/app/analytics/page.tsx` -- full page.
- Reuse `stat-card.tsx` component from overview.
- Recharts provides interactive tooltips, legends, responsive containers, and animation out of the box -- far superior to basic chart libraries.
- Date range change triggers re-fetch of both endpoints via TanStack Query key invalidation.

---

### 2.10 Suppression List Manager UI (US-7.6) -- 3 pts

**Route:** `/suppressions`

**Layout:**
- Search bar (shadcn `Input`) + reason filter (shadcn `Select`) + "Add Suppression" button.
- shadcn `Table` with Email, Reason (Badge), Source Message, Suppressed At, Delete action.
- Reason badges: `HardBounce` = red, `Complaint` = orange, `Manual` = blue.
- "Add Suppression" -> Dialog with email input + reason select.
- Delete -> confirmation Dialog -> calls remove endpoint, shows toast.

**Data Sources:**
- `GET /api/v1/suppressions?search={}&reason={}&page={}&page_size=50`
- `POST /api/v1/suppressions` (add)
- `DELETE /api/v1/suppressions/{id}` (remove)

**Implementation:**
- `src/app/suppressions/page.tsx` -- list page.
- `src/components/suppressions/add-suppression-dialog.tsx` -- Dialog with email + reason inputs.

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

**--- Backend complete. Dashboard begins. ---**

| # | Feature | Story IDs | Est. Hours | Dependencies | Rationale |
|---|---------|-----------|------------|--------------|-----------|
| 5 | **Dashboard Scaffolding** | -- | 2h | None | Next.js project, Tailwind, shadcn/ui, TanStack Query, API client, auth, layout, Dockerfile, docker-compose update. |
| 6 | **Dashboard Overview** | US-7.1 | 2.5h | Analytics API (#2), Scaffolding (#5) | First real page. Establishes component patterns (StatCard, charts). |
| 7 | **Email Log Viewer** | US-7.2 | 3h | Scaffolding (#5) | Highest-value dashboard page. Server-side table + detail drawer. |
| 8 | **Suppression Manager UI** | US-7.6 | 1.5h | Scaffolding (#5) | Simple CRUD page. Uses patterns from email log viewer. |
| 9 | **Domain Manager UI** | US-7.4 | 2h | Scaffolding (#5) | Expandable rows + DNS record display. Medium complexity. |
| 10 | **Template Manager UI** | US-7.3 | 3h | Scaffolding (#5) | Most complex UI page (form dialogs, preview). |
| 11 | **Analytics Dashboard** | US-7.5 | 2.5h | Analytics API (#2), Scaffolding (#5) | Charts page. Reuses StatCard from overview. |

**Total estimated: ~25.5 hours of development time**

### Critical Path

```
Migration (#1) --> Analytics API (#2) --> Dashboard Overview (#6) --> Analytics Dashboard (#11)
                                     \
Dashboard Scaffolding (#5) ---------> Email Log Viewer (#7) --> Suppression (#8) --> Domain (#9) --> Template (#10)

Webhook CRUD (#3) --> Webhook Dispatch (#4)
```

Three parallel chains:
1. **Analytics chain:** Migration -> Analytics API -> Overview page -> Analytics page
2. **Dashboard pages chain:** Scaffolding -> Email Logs -> Suppressions -> Domains -> Templates
3. **Webhooks chain:** CRUD -> Dispatch (independent of dashboard)

### Phase Breakdown (for developer orientation)

**Phase 1: Dashboard Scaffolding (2h)**
- `npx create-next-app@latest dashboard --typescript --tailwind --app --src-dir`
- Initialize shadcn/ui: `npx shadcn@latest init` + install all listed components
- Install TanStack Query, Recharts, Lucide React, next-themes
- Create `src/lib/api.ts` -- typed fetch wrapper
- Create `src/lib/auth.ts` -- auth context + middleware
- Create `src/app/login/page.tsx` -- login form
- Create `src/app/api/auth/login/route.ts` -- auth API route (BCrypt compare)
- Create `src/components/layout/app-shell.tsx` -- sidebar + top bar
- Create `src/components/layout/sidebar.tsx` -- navigation links
- Create `src/types/index.ts` -- all TypeScript interfaces matching API DTOs
- Create `dashboard/Dockerfile` (multi-stage: node build -> node:alpine runtime)
- Update `docker-compose.yml` with Next.js dashboard service configuration

**Phase 2: Core Pages (6h)**
- Overview dashboard: stat cards (shadcn Card), Recharts charts, recent emails table
- Email log viewer: filter bar, data table with pagination, detail Sheet (side drawer)
- Template manager: CRUD dialogs, code textarea, preview iframe

**Phase 3: Remaining Pages (4h)**
- Domain manager: list, DNS records table, add/verify dialogs
- Analytics: KPI cards, Recharts time-series (LineChart), status donut (PieChart), template bar chart
- Suppression manager: search, add dialog, remove with confirmation

**Phase 4: Analytics API + Webhooks (6h)**
- Same as original spec -- these are backend, no frontend technology impact

### Priority Tiers

**Tier 1 -- Must Ship (core dashboard value):**
Items 1-2, 5-7 (migration, analytics API, scaffolding, overview, email logs)

**Tier 2 -- High Value:**
Items 3-4, 8-11 (webhooks, remaining dashboard pages)

**Tier 3 -- Can Defer to Sprint 4:**
- Code editor for templates (Monaco/CodeMirror)
- Template version history diff view
- CSV export for suppressions
- Webhook delivery log viewer in dashboard
- Real-time updates via WebSocket/SSE

---

## 5. Staff Engineer Review (Gate 1)

### 5.1 Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Next.js SSR complexity** | Low | Use `"use client"` for all data-fetching pages (stat cards, tables, charts). Server Components only for static layout shells. Keep it simple -- no RSC data fetching patterns that could confuse debugging. |
| **CORS between Next.js and .NET API** | Medium | Two options: (a) Proxy API calls through Next.js API routes (`/api/proxy/[...path]`) to avoid CORS entirely, or (b) configure CORS on the .NET API to allow the dashboard origin. **Option (a) is preferred** -- it keeps the API key server-side and avoids exposing it to the browser. |
| **Dashboard-to-API auth gap** | Medium | Dashboard calls API internally without an API key. The API requires `Authorization: Bearer` header. **REQUIRED FIX:** Create a system-level API key for the dashboard and inject it into the Next.js server-side proxy. The API key is stored as `DASHBOARD_API_KEY` env var in the dashboard container. Never exposed to the browser. All API calls go through Next.js API routes which attach the key server-side. |
| **WebhookProcessor needs RabbitMQ** | Low | WebhookProcessor currently has no MassTransit dependency. Adding it is straightforward but increases its memory footprint. The 128MB container limit should be bumped to 192MB. |
| **Analytics query performance** | Low | Aggregating over the entire emails table with `GROUP BY` could be slow at scale. For Sprint 3 volumes (<10K emails), direct queries are fine. Add a materialized view or pre-computed daily aggregates in Sprint 4 if needed. The `idx_emails_analytics` index covers the hot path. |
| **Webhook retry storms** | Low | If a user's endpoint is permanently down, 5 retries x N events could create a retry backlog. MassTransit's built-in retry with exponential backoff handles this well. After final failure, the message goes to the error queue, not re-retried. |
| **Node.js memory on CX22** | Low | Next.js standalone output is lightweight (~30MB). The 192MB container limit is generous. Monitor with `docker stats`. |

### 5.2 Architecture Concerns

1. **Dashboard-API communication pattern:** The dashboard calls the API over HTTP via Next.js API route proxies, not directly from the browser. This keeps the API key server-side and avoids CORS issues. The proxy pattern also allows the dashboard to add request logging and error normalization.

2. **Webhook dispatch decoupling:** Publishing `WebhookDispatchMessage` from WebhookProcessor to Worker via RabbitMQ is the right pattern. It keeps the webhook HTTP dispatch logic in the Worker (which already has retry infrastructure) rather than bloating the WebhookProcessor. The WebhookProcessor stays focused on inbound SNS + tracking.

3. **Template editor simplification:** The spec correctly defers Monaco/CodeMirror to Sprint 4. A monospace `<textarea>` is sufficient for editing HTML/Liquid templates. The preview dialog gives immediate feedback. This avoids significant bundle size increase.

4. **No real-time updates in Sprint 3:** The dashboard pages fetch data on load and on user action (filter, page change). No WebSocket/SSE push updates. TanStack Query provides `refetchInterval` if we want polling later, but for Sprint 3, manual refresh is acceptable.

5. **Why Next.js:** Next.js + shadcn/ui provides a vastly larger ecosystem (npm), better developer tooling (hot reload, React DevTools), superior charting (Recharts), and a cleaner separation from the .NET backend. This is the correct technology decision.

### 5.3 Required Changes (incorporated above)

1. **Dashboard API proxy:** All browser-to-API calls go through Next.js API routes (`src/app/api/proxy/[...path]/route.ts`). The proxy attaches the `DASHBOARD_API_KEY` header. No API key in the browser.
2. **WebhookProcessor memory:** Bump container limit from 128MB to 192MB in docker-compose.
3. **Analytics rate limits:** The analytics endpoints should have their own rate limit (10 requests/minute) since they run aggregation queries. Add to rate limiting middleware.
4. **Webhook delivery log retention:** Add a note that webhook delivery logs should be pruned after 30 days. Implement via the existing cleanup job pattern.
5. **Docker-compose update:** The dashboard service runs as a Node.js-based Next.js container (port 3000 internal, environment variables for `NEXT_PUBLIC_API_URL`, `DASHBOARD_API_KEY`, `DASHBOARD_USERNAME`, `DASHBOARD_PASSWORD_HASH`).

### 5.4 Docker-Compose Dashboard Service (updated)

```yaml
dashboard:
  build:
    context: ./dashboard
    dockerfile: Dockerfile
  container_name: eaas-dashboard
  restart: unless-stopped
  environment:
    - NODE_ENV=production
    - NEXT_PUBLIC_API_URL=http://api:8080
    - DASHBOARD_API_KEY=${DASHBOARD_API_KEY}
    - DASHBOARD_USERNAME=${DASHBOARD_USERNAME}
    - DASHBOARD_PASSWORD_HASH=${DASHBOARD_PASSWORD_HASH}
  depends_on:
    api:
      condition: service_healthy
  ports:
    - "127.0.0.1:3000:3000"
  deploy:
    resources:
      limits:
        memory: 192M
```

### 5.5 Approval

**Gate 1 PASSED** with the 5 required changes above incorporated into the spec.

The scope is achievable in a 24-hour sprint because:
- 6 dashboard pages are UI-only (calling existing API endpoints via API proxy).
- Analytics API is 2 straightforward aggregation endpoints.
- Webhook CRUD is standard -- entity already exists.
- Webhook dispatch is the only complex piece, and MassTransit handles the heavy lifting (retry, DLQ).
- shadcn/ui provides pre-built, accessible, beautifully styled components (tables, dialogs, badges, cards, charts) that eliminate custom CSS work.
- Recharts provides interactive, responsive charts with tooltips and legends out of the box.
- TanStack Query handles caching, background refetch, loading/error states, and pagination seamlessly.
- Next.js App Router provides file-based routing, middleware auth, and API route proxies with zero configuration overhead.
