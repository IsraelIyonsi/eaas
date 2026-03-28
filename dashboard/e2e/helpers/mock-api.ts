import { Page } from "@playwright/test";

/**
 * Mock data for E2E tests.
 * Intercepts API proxy calls and returns mock data so tests work without a running backend.
 */

const mockAnalyticsSummary = {
  total_sent: 12847,
  delivered: 12340,
  bounced: 287,
  complained: 42,
  opened: 8234,
  clicked: 3612,
  failed: 178,
  delivery_rate: 96.05,
  open_rate: 66.73,
  click_rate: 29.27,
  bounce_rate: 2.23,
  complaint_rate: 0.34,
};

function generateTimeline() {
  const points = [];
  const now = new Date();
  for (let i = 29; i >= 0; i--) {
    const d = new Date(now);
    d.setDate(d.getDate() - i);
    const base = 400;
    points.push({
      timestamp: d.toISOString().split("T")[0] + "T00:00:00Z",
      sent: base,
      delivered: Math.floor(base * 0.95),
      bounced: Math.floor(base * 0.02),
      complained: Math.floor(base * 0.005),
      opened: Math.floor(base * 0.65),
      clicked: Math.floor(base * 0.35),
    });
  }
  return points;
}

const statuses = [
  "delivered",
  "delivered",
  "delivered",
  "delivered",
  "opened",
  "opened",
  "clicked",
  "bounced",
  "queued",
  "failed",
];
const subjects = [
  "Invoice #INV-2026-0042",
  "Payment Confirmation",
  "Welcome to CashTrack",
  "Your Weekly Report",
  "Password Reset Request",
  "Order Shipped - #ORD-8847",
  "Subscription Renewal Notice",
  "New Feature: Batch Sending",
  "Account Verification",
  "Monthly Statement - March 2026",
];
const recipients = [
  "john@example.com",
  "sarah@cashtrack.ng",
  "dev@acme.co",
  "billing@startup.io",
  "admin@techcorp.com",
];
const senders = [
  "noreply@cashtrack.ng",
  "notifications@cashtrack.ng",
  "billing@cashtrack.ng",
];

function generateEmails(count = 20) {
  const emails = [];
  for (let i = 0; i < count; i++) {
    const created = new Date();
    created.setHours(created.getHours() - i * 2);
    const status = statuses[i % statuses.length];
    emails.push({
      id: `email-${String(i + 1).padStart(3, "0")}`,
      message_id: `eaas_${Math.random().toString(36).slice(2, 14)}`,
      from: senders[i % senders.length],
      to: recipients[i % recipients.length],
      subject: subjects[i % subjects.length],
      status,
      template_name: i % 3 === 0 ? "Invoice Notification" : undefined,
      tags: i % 4 === 0 ? ["transactional"] : undefined,
      html_body: `<html><body><h1>${subjects[i % subjects.length]}</h1><p>Test body</p></body></html>`,
      text_body: `${subjects[i % subjects.length]}\n\nTest body`,
      created_at: created.toISOString(),
      sent_at:
        status !== "queued"
          ? new Date(created.getTime() + 2000).toISOString()
          : undefined,
      delivered_at: ["delivered", "opened", "clicked"].includes(status)
        ? new Date(created.getTime() + 5000).toISOString()
        : undefined,
    });
  }
  return emails;
}

function generateEvents(emailId: string) {
  const now = new Date();
  return [
    {
      id: `evt-${emailId}-1`,
      email_id: emailId,
      event_type: "queued",
      timestamp: new Date(now.getTime() - 10000).toISOString(),
      details: "Email accepted and queued for delivery",
    },
    {
      id: `evt-${emailId}-2`,
      email_id: emailId,
      event_type: "sending",
      timestamp: new Date(now.getTime() - 8000).toISOString(),
      details: "Sent to SES for delivery",
    },
    {
      id: `evt-${emailId}-3`,
      email_id: emailId,
      event_type: "delivered",
      timestamp: new Date(now.getTime() - 5000).toISOString(),
      details: "Delivered to recipient mail server",
    },
  ];
}

const mockTemplates = [
  {
    id: "tpl-001",
    name: "Invoice Notification",
    subject: "Invoice #{{invoice_number}} from {{company_name}}",
    html_body:
      "<html><body><h1>Invoice</h1><p>Amount: {{amount}}</p></body></html>",
    text_body: "Invoice #{{invoice_number}} - Amount: {{amount}}",
    version: 3,
    created_at: "2026-03-01T10:00:00Z",
    updated_at: "2026-03-25T14:30:00Z",
  },
  {
    id: "tpl-002",
    name: "Welcome Email",
    subject: "Welcome to {{app_name}}, {{first_name}}!",
    html_body:
      "<html><body><h1>Welcome!</h1><p>Thanks for signing up.</p></body></html>",
    text_body: "Welcome to {{app_name}}!",
    version: 1,
    created_at: "2026-03-10T08:00:00Z",
    updated_at: "2026-03-10T08:00:00Z",
  },
];

const mockDomains = [
  {
    id: "dom-001",
    domain: "mail.example.com",
    status: "verified",
    verified_at: "2026-03-15T12:00:00Z",
    dns_records: [
      {
        type: "TXT",
        name: "mail.example.com",
        value: "v=spf1 include:amazonses.com ~all",
        status: "verified",
      },
      {
        type: "CNAME",
        name: "eaas._domainkey.mail.example.com",
        value: "eaas.dkim.amazonses.com",
        status: "verified",
      },
      {
        type: "TXT",
        name: "_dmarc.mail.example.com",
        value: "v=DMARC1; p=quarantine;",
        status: "verified",
      },
    ],
    created_at: "2026-03-10T09:00:00Z",
  },
  {
    id: "dom-002",
    domain: "notifications.example.com",
    status: "pending_verification",
    dns_records: [
      {
        type: "TXT",
        name: "notifications.example.com",
        value: "v=spf1 include:amazonses.com ~all",
        status: "missing",
      },
    ],
    created_at: "2026-03-20T11:00:00Z",
  },
];

const mockSuppressions = [
  {
    id: "sup-001",
    email: "bounced@invalid.com",
    reason: "hard_bounce",
    created_at: "2026-03-20T10:00:00Z",
  },
  {
    id: "sup-002",
    email: "complainer@example.com",
    reason: "complaint",
    created_at: "2026-03-19T08:00:00Z",
  },
  {
    id: "sup-003",
    email: "manual@blocked.org",
    reason: "manual",
    created_at: "2026-03-18T15:00:00Z",
  },
];

const mockHealth = {
  status: "healthy",
  services: [
    { name: "API", status: "healthy", latency_ms: 12 },
    { name: "Worker", status: "healthy", latency_ms: 8 },
    { name: "RabbitMQ", status: "healthy", latency_ms: 5 },
    { name: "PostgreSQL", status: "healthy", latency_ms: 3 },
    { name: "Redis", status: "healthy", latency_ms: 1 },
  ],
};

const allEmails = generateEmails(20);

/**
 * Intercept all API proxy routes and return mock data.
 * Call this before navigating to pages that load data.
 */
export async function setupMockApi(page: Page) {
  await page.route("**/api/proxy/**", async (route) => {
    const url = route.request().url();
    const method = route.request().method();

    // Analytics summary
    if (url.includes("/api/v1/analytics/summary")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockAnalyticsSummary }),
      });
    }

    // Analytics timeline
    if (url.includes("/api/v1/analytics/timeline")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: { granularity: "day", points: generateTimeline() },
        }),
      });
    }

    // Email events
    if (url.match(/\/api\/v1\/emails\/[^/]+\/events/)) {
      const emailId = url.match(/\/emails\/([^/]+)\/events/)?.[1] ?? "";
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: generateEvents(emailId),
        }),
      });
    }

    // Single email
    if (url.match(/\/api\/v1\/emails\/[^/]+$/) && method === "GET") {
      const emailId = url.match(/\/emails\/([^/]+)$/)?.[1] ?? "";
      const email = allEmails.find((e) => e.id === emailId);
      return route.fulfill({
        status: email ? 200 : 404,
        contentType: "application/json",
        body: JSON.stringify({
          success: !!email,
          data: email,
        }),
      });
    }

    // Emails list
    if (url.includes("/api/v1/emails") && method === "GET") {
      const u = new URL(url);
      let filtered = [...allEmails];
      const status = u.searchParams.get("status");
      if (status) {
        filtered = filtered.filter((e) => e.status === status);
      }
      const search = u.searchParams.get("to");
      if (search) {
        const q = search.toLowerCase();
        filtered = filtered.filter(
          (e) =>
            e.to.toLowerCase().includes(q) ||
            e.subject.toLowerCase().includes(q)
        );
      }
      const page = parseInt(u.searchParams.get("page") ?? "1");
      const pageSize = parseInt(u.searchParams.get("page_size") ?? "10");
      const start = (page - 1) * pageSize;
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: filtered.slice(start, start + pageSize),
            total: filtered.length,
            page,
            page_size: pageSize,
            total_pages: Math.ceil(filtered.length / pageSize),
          },
        }),
      });
    }

    // Templates
    if (url.includes("/api/v1/templates") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: mockTemplates,
            total: mockTemplates.length,
            page: 1,
            page_size: 20,
            total_pages: 1,
          },
        }),
      });
    }

    if (url.includes("/api/v1/templates") && method === "POST") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `tpl-${Date.now()}`,
            version: 1,
            created_at: new Date().toISOString(),
            updated_at: new Date().toISOString(),
            ...JSON.parse(route.request().postData() ?? "{}"),
          },
        }),
      });
    }

    // Domains
    if (url.includes("/api/v1/domains") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: mockDomains,
            total: mockDomains.length,
            page: 1,
            page_size: 20,
            total_pages: 1,
          },
        }),
      });
    }

    if (url.includes("/api/v1/domains") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `dom-${Date.now()}`,
            domain: body.domain,
            status: "pending_verification",
            dns_records: [
              {
                type: "TXT",
                name: body.domain,
                value: "v=spf1 include:amazonses.com ~all",
                status: "missing",
              },
            ],
            created_at: new Date().toISOString(),
          },
        }),
      });
    }

    // Suppressions
    if (url.includes("/api/v1/suppressions") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: mockSuppressions,
            total: mockSuppressions.length,
            page: 1,
            page_size: 20,
            total_pages: 1,
          },
        }),
      });
    }

    if (url.includes("/api/v1/suppressions") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `sup-${Date.now()}`,
            email: body.email,
            reason: body.reason,
            created_at: new Date().toISOString(),
          },
        }),
      });
    }

    if (url.includes("/api/v1/suppressions") && method === "DELETE") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Health
    if (url.includes("/health")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(mockHealth),
      });
    }

    // Fallback
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ success: true, data: {} }),
    });
  });
}
