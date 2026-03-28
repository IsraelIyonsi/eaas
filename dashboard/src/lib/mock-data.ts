import type {
  Email,
  EmailEvent,
  Template,
  Domain,
  AnalyticsSummary,
  TimelinePoint,
  Suppression,
  SystemHealth,
} from "@/types";

// --- Analytics Summary ---
export const mockAnalyticsSummary: AnalyticsSummary = {
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

// --- Timeline (30 days) ---
function generateTimeline(): TimelinePoint[] {
  const points: TimelinePoint[] = [];
  const now = new Date();
  for (let i = 29; i >= 0; i--) {
    const d = new Date(now);
    d.setDate(d.getDate() - i);
    const base = 300 + Math.floor(Math.random() * 200);
    const delivered = Math.floor(base * (0.92 + Math.random() * 0.06));
    const bounced = Math.floor(base * (0.01 + Math.random() * 0.03));
    const complained = Math.floor(base * Math.random() * 0.008);
    const opened = Math.floor(delivered * (0.55 + Math.random() * 0.2));
    const clicked = Math.floor(opened * (0.3 + Math.random() * 0.15));
    points.push({
      timestamp: d.toISOString().split("T")[0] + "T00:00:00Z",
      sent: base,
      delivered,
      bounced,
      complained,
      opened,
      clicked,
    });
  }
  return points;
}

export const mockTimeline: TimelinePoint[] = generateTimeline();

// --- Emails ---
const statuses: Email["status"][] = [
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
  "complained",
  "sending",
];

function generateEmails(): Email[] {
  const emails: Email[] = [];
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
    "Team Invitation from Israel",
    "API Key Expiration Warning",
    "Delivery Failure Notification",
    "Domain Verification Required",
    "Security Alert: New Login",
    "Upgrade to Pro Plan",
    "Receipt for Payment #PAY-3321",
    "Export Complete: suppressions.csv",
    "Bounce Rate Alert",
    "Template Updated: Welcome Email",
  ];
  const recipients = [
    "john@example.com",
    "sarah@cashtrack.ng",
    "dev@acme.co",
    "billing@startup.io",
    "admin@techcorp.com",
    "user@demo.org",
    "test@mail.dev",
    "ops@infra.io",
    "support@helpdesk.ng",
    "marketing@brand.com",
  ];
  const senders = [
    "noreply@cashtrack.ng",
    "notifications@cashtrack.ng",
    "billing@cashtrack.ng",
    "noreply@mail.israeliyonsi.dev",
  ];

  for (let i = 0; i < 50; i++) {
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
      template_name:
        i % 3 === 0 ? "Invoice Notification" : i % 3 === 1 ? "Welcome Email" : undefined,
      tags: i % 4 === 0 ? ["transactional"] : i % 4 === 1 ? ["marketing"] : undefined,
      html_body: `<html><body><h1>${subjects[i % subjects.length]}</h1><p>This is a test email body with tracking pixel and content.</p></body></html>`,
      text_body: `${subjects[i % subjects.length]}\n\nThis is a test email body.`,
      created_at: created.toISOString(),
      sent_at: status !== "queued" ? new Date(created.getTime() + 2000).toISOString() : undefined,
      delivered_at:
        ["delivered", "opened", "clicked"].includes(status)
          ? new Date(created.getTime() + 5000).toISOString()
          : undefined,
      opened_at:
        ["opened", "clicked"].includes(status)
          ? new Date(created.getTime() + 300000).toISOString()
          : undefined,
      clicked_at:
        status === "clicked"
          ? new Date(created.getTime() + 600000).toISOString()
          : undefined,
    });
  }
  return emails;
}

export const mockEmails: Email[] = generateEmails();

export function getMockEmailEvents(email: Email): EmailEvent[] {
  const events: EmailEvent[] = [
    {
      id: `evt-${email.id}-1`,
      email_id: email.id,
      event_type: "queued",
      timestamp: email.created_at,
      details: "Email accepted by API and enqueued",
    },
  ];
  if (email.sent_at) {
    events.push({
      id: `evt-${email.id}-2`,
      email_id: email.id,
      event_type: "sending",
      timestamp: email.sent_at,
      details: "Picked up by worker, delivering to SES",
    });
  }
  if (email.delivered_at) {
    events.push({
      id: `evt-${email.id}-3`,
      email_id: email.id,
      event_type: "delivered",
      timestamp: email.delivered_at,
      details: "Confirmed delivered by recipient mail server",
    });
  }
  if (email.opened_at) {
    events.push({
      id: `evt-${email.id}-4`,
      email_id: email.id,
      event_type: "opened",
      timestamp: email.opened_at,
      details: "Tracking pixel loaded by recipient",
    });
  }
  if (email.clicked_at) {
    events.push({
      id: `evt-${email.id}-5`,
      email_id: email.id,
      event_type: "clicked",
      timestamp: email.clicked_at,
      details: "Recipient clicked a tracked link",
    });
  }
  if (email.status === "bounced") {
    events.push({
      id: `evt-${email.id}-b`,
      email_id: email.id,
      event_type: "bounced",
      timestamp: new Date(new Date(email.created_at).getTime() + 10000).toISOString(),
      details: "550 5.1.1 Recipient address rejected",
    });
  }
  if (email.status === "failed") {
    events.push({
      id: `evt-${email.id}-f`,
      email_id: email.id,
      event_type: "failed",
      timestamp: new Date(new Date(email.created_at).getTime() + 3000).toISOString(),
      details: "Template rendering error: missing variable 'customer_name'",
    });
  }
  if (email.status === "complained") {
    events.push({
      id: `evt-${email.id}-c`,
      email_id: email.id,
      event_type: "complained",
      timestamp: new Date(new Date(email.created_at).getTime() + 86400000).toISOString(),
      details: "Recipient marked email as spam",
    });
  }
  return events;
}

// --- Templates ---
export const mockTemplates: Template[] = [
  {
    id: "tpl-001",
    name: "Invoice Notification",
    subject: "Invoice #{{invoice_number}} from {{company_name}}",
    html_body: `<html><body style="font-family:sans-serif;padding:20px"><h1>Invoice #{{invoice_number}}</h1><p>Dear {{customer_name}},</p><p>A new invoice for <strong>{{amount}}</strong> has been generated.</p><p>Due date: {{due_date}}</p><a href="{{invoice_url}}" style="display:inline-block;padding:12px 24px;background:#7C4DFF;color:#fff;text-decoration:none;border-radius:6px">View Invoice</a></body></html>`,
    text_body:
      "Invoice #{{invoice_number}}\n\nDear {{customer_name}},\n\nA new invoice for {{amount}} has been generated.\nDue date: {{due_date}}\n\nView: {{invoice_url}}",
    variables_schema: JSON.stringify({
      invoice_number: "string",
      customer_name: "string",
      amount: "string",
      due_date: "string",
      company_name: "string",
      invoice_url: "string",
    }),
    version: 3,
    created_at: "2026-02-15T10:00:00Z",
    updated_at: "2026-03-20T14:30:00Z",
  },
  {
    id: "tpl-002",
    name: "Payment Confirmation",
    subject: "Payment received - {{amount}}",
    html_body: `<html><body style="font-family:sans-serif;padding:20px"><h1>Payment Confirmed</h1><p>Hi {{customer_name}},</p><p>We have received your payment of <strong>{{amount}}</strong>.</p><p>Transaction ID: {{transaction_id}}</p><p>Thank you for your business!</p></body></html>`,
    text_body:
      "Payment Confirmed\n\nHi {{customer_name}},\n\nWe have received your payment of {{amount}}.\nTransaction ID: {{transaction_id}}\n\nThank you!",
    variables_schema: JSON.stringify({
      customer_name: "string",
      amount: "string",
      transaction_id: "string",
    }),
    version: 2,
    created_at: "2026-02-20T09:00:00Z",
    updated_at: "2026-03-18T11:00:00Z",
  },
  {
    id: "tpl-003",
    name: "Welcome Email",
    subject: "Welcome to {{app_name}}!",
    html_body: `<html><body style="font-family:sans-serif;padding:20px"><h1>Welcome, {{user_name}}!</h1><p>Thanks for signing up for {{app_name}}. We are excited to have you on board.</p><p>Here are some things you can do to get started:</p><ul><li>Set up your profile</li><li>Create your first project</li><li>Invite your team</li></ul><a href="{{dashboard_url}}" style="display:inline-block;padding:12px 24px;background:#7C4DFF;color:#fff;text-decoration:none;border-radius:6px">Go to Dashboard</a></body></html>`,
    text_body:
      "Welcome, {{user_name}}!\n\nThanks for signing up for {{app_name}}.\n\nGet started:\n- Set up your profile\n- Create your first project\n- Invite your team\n\nDashboard: {{dashboard_url}}",
    variables_schema: JSON.stringify({
      user_name: "string",
      app_name: "string",
      dashboard_url: "string",
    }),
    version: 1,
    created_at: "2026-03-01T08:00:00Z",
    updated_at: "2026-03-01T08:00:00Z",
  },
];

// --- Domains ---
export const mockDomains: Domain[] = [
  {
    id: "dom-001",
    domain: "cashtrack.ng",
    status: "verified",
    dns_records: [
      {
        type: "TXT",
        name: "cashtrack.ng",
        value: "v=spf1 include:amazonses.com ~all",
        status: "verified",
      },
      {
        type: "CNAME",
        name: "eaas._domainkey.cashtrack.ng",
        value: "eaas.dkim.amazonses.com",
        status: "verified",
      },
      {
        type: "TXT",
        name: "_dmarc.cashtrack.ng",
        value: "v=DMARC1; p=quarantine; rua=mailto:dmarc@cashtrack.ng",
        status: "verified",
      },
    ],
    verified_at: "2026-03-10T12:00:00Z",
    created_at: "2026-03-09T10:00:00Z",
  },
  {
    id: "dom-002",
    domain: "mail.israeliyonsi.dev",
    status: "pending_verification",
    dns_records: [
      {
        type: "TXT",
        name: "mail.israeliyonsi.dev",
        value: "v=spf1 include:amazonses.com ~all",
        status: "missing",
      },
      {
        type: "CNAME",
        name: "eaas._domainkey.mail.israeliyonsi.dev",
        value: "eaas.dkim.amazonses.com",
        status: "missing",
      },
      {
        type: "TXT",
        name: "_dmarc.mail.israeliyonsi.dev",
        value: "v=DMARC1; p=none; rua=mailto:dmarc@israeliyonsi.dev",
        status: "missing",
      },
    ],
    created_at: "2026-03-25T15:00:00Z",
  },
  {
    id: "dom-003",
    domain: "notifications.acme.co",
    status: "failed",
    dns_records: [
      {
        type: "TXT",
        name: "notifications.acme.co",
        value: "v=spf1 include:amazonses.com ~all",
        status: "verified",
      },
      {
        type: "CNAME",
        name: "eaas._domainkey.notifications.acme.co",
        value: "eaas.dkim.amazonses.com",
        status: "mismatch",
      },
      {
        type: "TXT",
        name: "_dmarc.notifications.acme.co",
        value: "v=DMARC1; p=quarantine; rua=mailto:dmarc@acme.co",
        status: "missing",
      },
    ],
    created_at: "2026-03-22T11:00:00Z",
  },
];

// --- Suppressions ---
export const mockSuppressions: Suppression[] = [
  {
    id: "sup-001",
    email: "invalid@nonexistent.com",
    reason: "hard_bounce",
    created_at: "2026-03-15T10:00:00Z",
  },
  {
    id: "sup-002",
    email: "spammer@darkweb.org",
    reason: "complaint",
    created_at: "2026-03-18T14:30:00Z",
  },
  {
    id: "sup-003",
    email: "old@defunct-company.com",
    reason: "soft_bounce_limit",
    created_at: "2026-03-20T09:15:00Z",
  },
  {
    id: "sup-004",
    email: "donotcontact@privacy.com",
    reason: "manual",
    created_at: "2026-03-22T16:00:00Z",
  },
  {
    id: "sup-005",
    email: "bounced@bad-mx.net",
    reason: "hard_bounce",
    created_at: "2026-03-23T11:45:00Z",
  },
  {
    id: "sup-006",
    email: "complainer@inbox.com",
    reason: "complaint",
    created_at: "2026-03-24T08:20:00Z",
  },
  {
    id: "sup-007",
    email: "test@removed-domain.io",
    reason: "hard_bounce",
    created_at: "2026-03-25T13:00:00Z",
  },
  {
    id: "sup-008",
    email: "unsubscribed@user.co",
    reason: "manual",
    created_at: "2026-03-26T10:00:00Z",
  },
];

// --- System Health ---
export const mockSystemHealth: SystemHealth = {
  status: "healthy",
  services: [
    { name: "API", status: "healthy", latency_ms: 12 },
    { name: "Worker", status: "healthy", latency_ms: 8 },
    { name: "RabbitMQ", status: "healthy", latency_ms: 3 },
    { name: "PostgreSQL", status: "healthy", latency_ms: 5 },
    { name: "Redis", status: "healthy", latency_ms: 1 },
  ],
};
