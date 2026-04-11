import { Page } from "@playwright/test";

/**
 * Mock data for E2E tests.
 * Intercepts API proxy calls and returns mock data so tests work without a running backend.
 *
 * Field names use camelCase to match the TypeScript types that the frontend expects.
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
      messageId: `eaas_${Math.random().toString(36).slice(2, 14)}`,
      from: senders[i % senders.length],
      to: recipients[i % recipients.length],
      subject: subjects[i % subjects.length],
      status,
      templateName: i % 3 === 0 ? "Invoice Notification" : undefined,
      tags: i % 4 === 0 ? ["transactional"] : undefined,
      htmlBody: `<html><body><h1>${subjects[i % subjects.length]}</h1><p>Test body</p></body></html>`,
      textBody: `${subjects[i % subjects.length]}\n\nTest body`,
      createdAt: created.toISOString(),
      sentAt:
        status !== "queued"
          ? new Date(created.getTime() + 2000).toISOString()
          : undefined,
      deliveredAt: ["delivered", "opened", "clicked"].includes(status)
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
      emailId: emailId,
      eventType: "queued",
      timestamp: new Date(now.getTime() - 10000).toISOString(),
      details: "Email accepted and queued for delivery",
    },
    {
      id: `evt-${emailId}-2`,
      emailId: emailId,
      eventType: "sending",
      timestamp: new Date(now.getTime() - 8000).toISOString(),
      details: "Sent to SES for delivery",
    },
    {
      id: `evt-${emailId}-3`,
      emailId: emailId,
      eventType: "delivered",
      timestamp: new Date(now.getTime() - 5000).toISOString(),
      details: "Delivered to recipient mail server",
    },
  ];
}

const mockTemplates = [
  {
    id: "tpl-001",
    name: "Invoice Notification",
    subjectTemplate: "Invoice #{{invoice_number}} from {{company_name}}",
    htmlBody:
      "<html><body><h1>Invoice</h1><p>Amount: {{amount}}</p></body></html>",
    textBody: "Invoice #{{invoice_number}} - Amount: {{amount}}",
    version: 3,
    createdAt: "2026-03-01T10:00:00Z",
    updatedAt: "2026-03-25T14:30:00Z",
  },
  {
    id: "tpl-002",
    name: "Welcome Email",
    subjectTemplate: "Welcome to {{app_name}}, {{first_name}}!",
    htmlBody:
      "<html><body><h1>Welcome!</h1><p>Thanks for signing up.</p></body></html>",
    textBody: "Welcome to {{app_name}}!",
    version: 1,
    createdAt: "2026-03-10T08:00:00Z",
    updatedAt: "2026-03-10T08:00:00Z",
  },
];

const mockDomains = [
  {
    id: "dom-001",
    domainName: "mail.example.com",
    status: "Verified",
    verifiedAt: "2026-03-15T12:00:00Z",
    dnsRecords: [
      {
        type: "TXT",
        name: "mail.example.com",
        value: "v=spf1 include:amazonses.com ~all",
        purpose: "SPF",
        isVerified: true,
      },
      {
        type: "CNAME",
        name: "eaas._domainkey.mail.example.com",
        value: "eaas.dkim.amazonses.com",
        purpose: "DKIM",
        isVerified: true,
      },
      {
        type: "TXT",
        name: "_dmarc.mail.example.com",
        value: "v=DMARC1; p=quarantine;",
        purpose: "DMARC",
        isVerified: true,
      },
    ],
    inbound_enabled: true,
    inbound_rule_count: 2,
    createdAt: "2026-03-10T09:00:00Z",
  },
  {
    id: "dom-002",
    domainName: "notifications.example.com",
    status: "PendingVerification",
    dnsRecords: [
      {
        type: "TXT",
        name: "notifications.example.com",
        value: "v=spf1 include:amazonses.com ~all",
        purpose: "SPF",
        isVerified: false,
      },
    ],
    inbound_enabled: false,
    inbound_rule_count: 0,
    createdAt: "2026-03-20T11:00:00Z",
  },
];

const mockSuppressions = [
  {
    id: "sup-001",
    emailAddress: "bounced@invalid.com",
    reason: "hard_bounce",
    suppressedAt: "2026-03-20T10:00:00Z",
  },
  {
    id: "sup-002",
    emailAddress: "complainer@example.com",
    reason: "complaint",
    suppressedAt: "2026-03-19T08:00:00Z",
  },
  {
    id: "sup-003",
    emailAddress: "manual@blocked.org",
    reason: "manual",
    suppressedAt: "2026-03-18T15:00:00Z",
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

const mockInboundEmails = [
  {
    id: "inbound-001",
    messageId: "<msg-001@example.com>",
    fromEmail: "john@example.com",
    fromName: "John Doe",
    toEmails: [{ email: "support@myapp.com", name: "" }],
    ccEmails: [],
    subject: "Re: Order #12345",
    status: "processed",
    spamVerdict: "pass",
    virusVerdict: "pass",
    spfVerdict: "pass",
    dkimVerdict: "pass",
    dmarcVerdict: "pass",
    attachments: [
      {
        id: "att-001",
        filename: "invoice.pdf",
        contentType: "application/pdf",
        sizeBytes: 45000,
        s3Key: "inbound/att-001",
        isInline: false,
      },
    ],
    receivedAt: new Date().toISOString(),
    createdAt: new Date().toISOString(),
  },
  {
    id: "inbound-002",
    messageId: "<msg-002@example.com>",
    fromEmail: "jane@company.org",
    fromName: "Jane Smith",
    toEmails: [{ email: "billing@myapp.com", name: "" }],
    ccEmails: [],
    subject: "Payment inquiry",
    status: "received",
    spamVerdict: "pass",
    virusVerdict: "pass",
    spfVerdict: "pass",
    dkimVerdict: "pass",
    dmarcVerdict: "pass",
    attachments: [],
    receivedAt: new Date().toISOString(),
    createdAt: new Date().toISOString(),
  },
  {
    id: "inbound-003",
    messageId: "<msg-003@example.com>",
    fromEmail: "mike@external.io",
    fromName: "Mike Johnson",
    toEmails: [{ email: "info@myapp.com", name: "" }],
    ccEmails: [{ email: "cc@myapp.com", name: "" }],
    subject: "Partnership proposal",
    status: "processing",
    spamVerdict: "pass",
    virusVerdict: "pass",
    spfVerdict: "pass",
    dkimVerdict: "pass",
    dmarcVerdict: "pass",
    attachments: [],
    receivedAt: new Date().toISOString(),
    createdAt: new Date().toISOString(),
  },
  {
    id: "inbound-004",
    messageId: "<msg-004@example.com>",
    fromEmail: "admin@partner.co",
    fromName: "Admin",
    toEmails: [{ email: "support@myapp.com", name: "" }],
    ccEmails: [],
    subject: "Forwarding test",
    status: "forwarded",
    spamVerdict: "pass",
    virusVerdict: "pass",
    spfVerdict: "pass",
    dkimVerdict: "pass",
    dmarcVerdict: "pass",
    attachments: [],
    receivedAt: new Date().toISOString(),
    createdAt: new Date().toISOString(),
  },
  {
    id: "inbound-005",
    messageId: "<msg-005@example.com>",
    fromEmail: "spam@bad-actor.net",
    fromName: "Spammer",
    toEmails: [{ email: "catchall@myapp.com", name: "" }],
    ccEmails: [],
    subject: "You won a prize!",
    status: "failed",
    spamVerdict: "fail",
    virusVerdict: "pass",
    spfVerdict: "fail",
    dkimVerdict: "fail",
    dmarcVerdict: "fail",
    attachments: [],
    receivedAt: new Date().toISOString(),
    createdAt: new Date().toISOString(),
  },
];

const mockInboundRules = [
  {
    id: "rule-001",
    name: "Support Inbox",
    domainId: "dom-001",
    domainName: "example.com",
    matchPattern: "support@",
    action: "webhook",
    webhookUrl: "https://myapp.com/hooks",
    isActive: true,
    priority: 0,
    createdAt: "2026-03-15T10:00:00Z",
    updatedAt: "2026-03-20T14:00:00Z",
  },
  {
    id: "rule-002",
    name: "Billing Forward",
    domainId: "dom-001",
    domainName: "example.com",
    matchPattern: "billing@",
    action: "forward",
    forwardTo: "accounting@company.com",
    isActive: true,
    priority: 1,
    createdAt: "2026-03-16T09:00:00Z",
    updatedAt: "2026-03-18T11:00:00Z",
  },
  {
    id: "rule-003",
    name: "Catch-All",
    domainId: "dom-001",
    domainName: "example.com",
    matchPattern: "*@",
    action: "store",
    isActive: false,
    priority: 99,
    createdAt: "2026-03-17T08:00:00Z",
    updatedAt: "2026-03-17T08:00:00Z",
  },
];

const mockApiKeys = [
  {
    id: "key-001",
    name: "Production Key",
    keyPrefix: "eaas_sk_prod_",
    isActive: true,
    createdAt: "2026-03-01T10:00:00Z",
  },
  {
    id: "key-002",
    name: "Staging Key",
    keyPrefix: "eaas_sk_stag_",
    isActive: true,
    createdAt: "2026-03-10T09:00:00Z",
  },
  {
    id: "key-003",
    name: "Old Key",
    keyPrefix: "eaas_sk_old_",
    isActive: false,
    createdAt: "2026-02-15T10:00:00Z",
  },
];

const mockWebhooks = [
  {
    id: "wh-001",
    url: "https://myapp.com/webhooks/email",
    events: ["email.delivered", "email.bounced", "email.failed"],
    status: "active",
    secret: "whsec_abc123def456",
    createdAt: "2026-03-10T10:00:00Z",
    updatedAt: "2026-03-25T14:00:00Z",
  },
  {
    id: "wh-002",
    url: "https://myapp.com/webhooks/tracking",
    events: ["email.opened", "email.clicked"],
    status: "active",
    secret: "whsec_xyz789",
    createdAt: "2026-03-12T09:00:00Z",
    updatedAt: "2026-03-22T11:00:00Z",
  },
];

const mockInboundAnalyticsSummary = {
  total_received: 3420,
  processed: 3180,
  failed: 84,
  spam_flagged: 120,
  virus_flagged: 36,
  processing_rate: 0.93,
  avg_processing_time_ms: 245,
};

function generateInboundTimeline() {
  const points = [];
  const now = new Date();
  for (let i = 6; i >= 0; i--) {
    const d = new Date(now);
    d.setDate(d.getDate() - i);
    points.push({
      timestamp: d.toISOString().split("T")[0] + "T00:00:00Z",
      sent: Math.floor(400 + Math.random() * 100),
    });
  }
  return points;
}

const mockTopSenders = [
  {
    email: "john@example.com",
    total_emails: 342,
    last_receivedAt: new Date().toISOString(),
  },
  {
    email: "jane@company.org",
    total_emails: 218,
    last_receivedAt: new Date().toISOString(),
  },
  {
    email: "mike@external.io",
    total_emails: 156,
    last_receivedAt: new Date().toISOString(),
  },
];

// Admin mock data
const mockAdminTenants = [
  {
    id: "tenant-001",
    name: "Acme Corporation",
    status: "active",
    companyName: "Acme Corp",
    contactEmail: "admin@acme.com",
    apiKeyCount: 3,
    domainCount: 2,
    emailCount: 5430,
    createdAt: "2026-01-15T10:00:00Z",
  },
  {
    id: "tenant-002",
    name: "Beta Industries",
    status: "active",
    companyName: "Beta Inc",
    contactEmail: "admin@beta.io",
    apiKeyCount: 1,
    domainCount: 1,
    emailCount: 1200,
    createdAt: "2026-02-01T08:00:00Z",
  },
  {
    id: "tenant-003",
    name: "Gamma Solutions",
    status: "suspended",
    companyName: "Gamma LLC",
    contactEmail: "admin@gamma.co",
    apiKeyCount: 2,
    domainCount: 1,
    emailCount: 890,
    createdAt: "2026-02-15T09:00:00Z",
  },
  {
    id: "tenant-004",
    name: "Delta Corp",
    status: "deactivated",
    companyName: "Delta Corp",
    contactEmail: "admin@delta.com",
    apiKeyCount: 0,
    domainCount: 0,
    emailCount: 0,
    createdAt: "2026-03-01T11:00:00Z",
  },
  {
    id: "tenant-005",
    name: "Epsilon Tech",
    status: "active",
    companyName: "Epsilon",
    contactEmail: "admin@epsilon.dev",
    apiKeyCount: 4,
    domainCount: 3,
    emailCount: 8700,
    createdAt: "2026-03-10T14:00:00Z",
  },
];

const mockAdminUsers = [
  {
    id: "admin-001",
    email: "superadmin@eaas.io",
    displayName: "Super Admin",
    role: "superadmin",
    isActive: true,
    lastLoginAt: "2026-03-30T10:00:00Z",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-03-30T10:00:00Z",
  },
  {
    id: "admin-002",
    email: "admin@eaas.io",
    displayName: "Regular Admin",
    role: "admin",
    isActive: true,
    lastLoginAt: "2026-03-28T15:00:00Z",
    createdAt: "2026-02-01T00:00:00Z",
    updatedAt: "2026-03-28T15:00:00Z",
  },
  {
    id: "admin-003",
    email: "readonly@eaas.io",
    displayName: "Read Only",
    role: "readonly",
    isActive: false,
    lastLoginAt: "2026-03-01T08:00:00Z",
    createdAt: "2026-03-01T00:00:00Z",
    updatedAt: "2026-03-15T12:00:00Z",
  },
];

const mockPlatformSummary = {
  totalTenants: 5,
  activeTenants: 3,
  totalEmails: 16220,
  totalDomains: 7,
  totalApiKeys: 10,
  totalAdminUsers: 3,
};

const mockTenantRankings = [
  {
    tenantId: "tenant-005",
    tenantName: "Epsilon Tech",
    company: "Epsilon",
    emailCount: 8700,
    domainCount: 3,
  },
  {
    tenantId: "tenant-001",
    tenantName: "Acme Corporation",
    company: "Acme Corp",
    emailCount: 5430,
    domainCount: 2,
  },
  {
    tenantId: "tenant-002",
    tenantName: "Beta Industries",
    company: "Beta Inc",
    emailCount: 1200,
    domainCount: 1,
  },
];

const mockGrowthMetrics = {
  newTenantsThisMonth: 2,
  newTenantsLastMonth: 1,
  emailsThisMonth: 4200,
  emailsLastMonth: 3800,
  tenantGrowthRate: 100.0,
  emailGrowthRate: 10.5,
};

const mockAuditLogs = [
  {
    id: "log-001",
    adminEmail: "superadmin@eaas.io",
    action: "tenant.create",
    targetType: "Tenant",
    targetId: "tenant-005",
    targetName: "Epsilon Tech",
    details: "{}",
    ipAddress: "192.168.1.1",
    createdAt: "2026-03-30T10:30:00Z",
  },
  {
    id: "log-002",
    adminEmail: "admin@eaas.io",
    action: "tenant.suspend",
    targetType: "Tenant",
    targetId: "tenant-003",
    targetName: "Gamma Solutions",
    details: '{"reason":"Policy violation"}',
    ipAddress: "192.168.1.2",
    createdAt: "2026-03-29T14:00:00Z",
  },
  {
    id: "log-003",
    adminEmail: "superadmin@eaas.io",
    action: "user.create",
    targetType: "AdminUser",
    targetId: "admin-003",
    targetName: "Read Only",
    details: "{}",
    ipAddress: "192.168.1.1",
    createdAt: "2026-03-28T09:00:00Z",
  },
  {
    id: "log-004",
    adminEmail: "superadmin@eaas.io",
    action: "tenant.create",
    targetType: "Tenant",
    targetId: "tenant-004",
    targetName: "Delta Corp",
    details: "{}",
    ipAddress: "192.168.1.1",
    createdAt: "2026-03-27T11:00:00Z",
  },
  {
    id: "log-005",
    adminEmail: "admin@eaas.io",
    action: "config.update",
    targetType: "Config",
    targetId: null,
    targetName: null,
    details: '{"key":"rate_limit"}',
    ipAddress: "192.168.1.2",
    createdAt: "2026-03-26T16:00:00Z",
  },
  {
    id: "log-006",
    adminEmail: "superadmin@eaas.io",
    action: "tenant.activate",
    targetType: "Tenant",
    targetId: "tenant-002",
    targetName: "Beta Industries",
    details: "{}",
    ipAddress: "192.168.1.1",
    createdAt: "2026-03-25T10:00:00Z",
  },
  {
    id: "log-007",
    adminEmail: "admin@eaas.io",
    action: "tenant.update",
    targetType: "Tenant",
    targetId: "tenant-001",
    targetName: "Acme Corporation",
    details: '{"field":"monthlyEmailLimit"}',
    ipAddress: "192.168.1.2",
    createdAt: "2026-03-24T13:00:00Z",
  },
  {
    id: "log-008",
    adminEmail: "superadmin@eaas.io",
    action: "user.delete",
    targetType: "AdminUser",
    targetId: "admin-old",
    targetName: "Old Admin",
    details: "{}",
    ipAddress: "192.168.1.1",
    createdAt: "2026-03-23T08:00:00Z",
  },
  {
    id: "log-009",
    adminEmail: "admin@eaas.io",
    action: "tenant.create",
    targetType: "Tenant",
    targetId: "tenant-003",
    targetName: "Gamma Solutions",
    details: "{}",
    ipAddress: "192.168.1.2",
    createdAt: "2026-03-22T10:00:00Z",
  },
  {
    id: "log-010",
    adminEmail: "superadmin@eaas.io",
    action: "tenant.create",
    targetType: "Tenant",
    targetId: "tenant-002",
    targetName: "Beta Industries",
    details: "{}",
    ipAddress: "192.168.1.1",
    createdAt: "2026-03-21T09:00:00Z",
  },
];

// Billing mock data
const mockPlans = [
  { id: "plan-free", name: "Free", tier: "free", monthlyPriceUsd: 0, annualPriceUsd: 0, dailyEmailLimit: 100, monthlyEmailLimit: 3000, maxApiKeys: 3, maxDomains: 2, maxTemplates: 10, maxWebhooks: 5, customDomainBranding: false, prioritySupport: false, isActive: true },
  { id: "plan-starter", name: "Starter", tier: "starter", monthlyPriceUsd: 9.99, annualPriceUsd: 99.99, dailyEmailLimit: 1000, monthlyEmailLimit: 30000, maxApiKeys: 5, maxDomains: 5, maxTemplates: 50, maxWebhooks: 10, customDomainBranding: false, prioritySupport: false, isActive: true },
  { id: "plan-pro", name: "Pro", tier: "pro", monthlyPriceUsd: 29.99, annualPriceUsd: 299.99, dailyEmailLimit: 10000, monthlyEmailLimit: 300000, maxApiKeys: 10, maxDomains: 10, maxTemplates: 200, maxWebhooks: 25, customDomainBranding: true, prioritySupport: false, isActive: true },
  { id: "plan-business", name: "Business", tier: "business", monthlyPriceUsd: 79.99, annualPriceUsd: 799.99, dailyEmailLimit: 50000, monthlyEmailLimit: 1500000, maxApiKeys: 25, maxDomains: 25, maxTemplates: 500, maxWebhooks: 50, customDomainBranding: true, prioritySupport: true, isActive: true },
  { id: "plan-enterprise", name: "Enterprise", tier: "enterprise", monthlyPriceUsd: 199.99, annualPriceUsd: 1999.99, dailyEmailLimit: 200000, monthlyEmailLimit: 6000000, maxApiKeys: 100, maxDomains: 100, maxTemplates: 2000, maxWebhooks: 200, customDomainBranding: true, prioritySupport: true, isActive: true },
];

const mockSubscription = {
  id: "sub-1",
  planId: "plan-free",
  planName: "Free",
  planTier: "free",
  status: "active",
  provider: "none",
  currentPeriodStart: "2026-04-01T00:00:00Z",
  currentPeriodEnd: "2026-05-01T00:00:00Z",
};

const mockInvoices = { items: [], totalCount: 0, page: 1, pageSize: 20 };

const mockAdminSystemHealth = {
  status: "healthy",
  services: [
    { name: "API Server", status: "healthy", latencyMs: 12, checkedAt: "2026-03-30T10:00:00Z" },
    { name: "PostgreSQL", status: "healthy", latencyMs: 3, checkedAt: "2026-03-30T10:00:00Z" },
    { name: "Redis", status: "healthy", latencyMs: 1, checkedAt: "2026-03-30T10:00:00Z" },
    { name: "RabbitMQ", status: "healthy", latencyMs: 5, checkedAt: "2026-03-30T10:00:00Z" },
    { name: "AWS SES", status: "healthy", latencyMs: 45, checkedAt: "2026-03-30T10:00:00Z" },
    { name: "S3 Storage", status: "healthy", latencyMs: 22, checkedAt: "2026-03-30T10:00:00Z" },
  ],
  metrics: {
    tenantCount: 5,
    totalEmailsSent: 16220,
    queueDepth: 12,
    avgLatencyMs: 15,
  },
  uptime: "15d 4h 32m",
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

    // Inbound Analytics summary
    if (url.includes("/api/v1/analytics/inbound/summary")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: mockInboundAnalyticsSummary,
        }),
      });
    }

    // Inbound Analytics timeline
    if (url.includes("/api/v1/analytics/inbound/timeline")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            granularity: "day",
            points: generateInboundTimeline(),
          },
        }),
      });
    }

    // Inbound Analytics top senders
    if (url.includes("/api/v1/analytics/inbound/top-senders")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockTopSenders }),
      });
    }

    // Analytics summary (outbound)
    if (url.includes("/api/v1/analytics/summary")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockAnalyticsSummary }),
      });
    }

    // Analytics timeline (outbound)
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

    // Inbound email delete
    if (
      url.match(/\/api\/v1\/inbound\/emails\/[^/]+$/) &&
      method === "DELETE"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Single inbound email
    if (
      url.match(/\/api\/v1\/inbound\/emails\/[^/]+$/) &&
      method === "GET"
    ) {
      const emailId =
        url.match(/\/inbound\/emails\/([^/]+)$/)?.[1] ?? "";
      const email = mockInboundEmails.find((e) => e.id === emailId);
      return route.fulfill({
        status: email ? 200 : 404,
        contentType: "application/json",
        body: JSON.stringify({ success: !!email, data: email }),
      });
    }

    // Inbound emails list
    if (url.includes("/api/v1/inbound/emails") && method === "GET") {
      const u = new URL(url);
      let filtered = [...mockInboundEmails];
      const status = u.searchParams.get("status");
      if (status) {
        filtered = filtered.filter((e) => e.status === status);
      }
      const from = u.searchParams.get("from");
      if (from) {
        const q = from.toLowerCase();
        filtered = filtered.filter(
          (e) =>
            e.fromEmail.toLowerCase().includes(q) ||
            e.fromName.toLowerCase().includes(q) ||
            (e.subject ?? "").toLowerCase().includes(q),
        );
      }
      const pg = parseInt(u.searchParams.get("page") ?? "1");
      const pageSize = parseInt(u.searchParams.get("page_size") ?? "10");
      const start = (pg - 1) * pageSize;
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: filtered.slice(start, start + pageSize),
            totalCount: filtered.length,
            page: pg,
            pageSize: pageSize,
          },
        }),
      });
    }

    // Inbound rules - delete
    if (
      url.match(/\/api\/v1\/inbound\/rules\/[^/]+$/) &&
      method === "DELETE"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Inbound rules - update
    if (
      url.match(/\/api\/v1\/inbound\/rules\/[^/]+$/) &&
      method === "PUT"
    ) {
      const ruleId =
        url.match(/\/inbound\/rules\/([^/]+)$/)?.[1] ?? "";
      const rule = mockInboundRules.find((r) => r.id === ruleId);
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: { ...rule, ...body, updatedAt: new Date().toISOString() },
        }),
      });
    }

    // Inbound rules - create
    if (url.includes("/api/v1/inbound/rules") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `rule-${Date.now()}`,
            ...body,
            isActive: true,
            priority: mockInboundRules.length,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
        }),
      });
    }

    // Inbound rules - list
    if (url.includes("/api/v1/inbound/rules") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: mockInboundRules,
            totalCount: mockInboundRules.length,
            page: 1,
            pageSize: 20,
          },
        }),
      });
    }

    // API Keys - revoke
    if (url.match(/\/api\/v1\/keys\/[^/]+\/revoke/) && method === "POST") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: { revoked: true, revokedAt: new Date().toISOString() },
        }),
      });
    }

    // API Keys - rotate
    if (url.match(/\/api\/v1\/keys\/[^/]+\/rotate/) && method === "POST") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            key: "eaas_sk_live_new_rotated_key_" + Date.now(),
            expiresOldKeyAt: new Date(
              Date.now() + 24 * 60 * 60 * 1000,
            ).toISOString(),
          },
        }),
      });
    }

    // API Keys - create
    if (url.includes("/api/v1/keys") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `key-${Date.now()}`,
            name: body.name,
            prefix: "eaas_sk_live_",
            key: "eaas_sk_live_full_key_shown_once_" + Date.now(),
            createdAt: new Date().toISOString(),
          },
        }),
      });
    }

    // API Keys - list
    if (url.includes("/api/v1/keys") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockApiKeys }),
      });
    }

    // Webhooks - test
    if (url.match(/\/api\/v1\/webhooks\/[^/]+\/test/) && method === "POST") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            success: true,
            statusCode: 200,
            responseTimeMs: 142,
          },
        }),
      });
    }

    // Webhooks - deliveries (logs)
    if (
      url.match(/\/api\/v1\/webhooks\/[^/]+\/deliveries/) &&
      method === "GET"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 10,
          },
        }),
      });
    }

    // Webhooks - delete
    if (
      url.match(/\/api\/v1\/webhooks\/[^/]+$/) &&
      method === "DELETE"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Webhooks - update
    if (
      url.match(/\/api\/v1\/webhooks\/[^/]+$/) &&
      method === "PUT"
    ) {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: { ...body, updatedAt: new Date().toISOString() },
        }),
      });
    }

    // Webhooks - create
    if (url.includes("/api/v1/webhooks") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `wh-${Date.now()}`,
            ...body,
            status: "active",
            secret: "whsec_new_" + Date.now(),
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
        }),
      });
    }

    // Webhooks - list
    if (url.includes("/api/v1/webhooks") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockWebhooks }),
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
      const pg = parseInt(u.searchParams.get("page") ?? "1");
      const pageSize = parseInt(u.searchParams.get("page_size") ?? "10");
      const start = (pg - 1) * pageSize;
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: filtered.slice(start, start + pageSize),
            totalCount: filtered.length,
            page: pg,
            pageSize: pageSize,
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
            totalCount: mockTemplates.length,
            page: 1,
            pageSize: 20,
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
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            ...JSON.parse(route.request().postData() ?? "{}"),
          },
        }),
      });
    }

    // Domain verify
    if (
      url.match(/\/api\/v1\/domains\/[^/]+\/verify/) &&
      method === "POST"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: { verified: true },
        }),
      });
    }

    // Domain delete
    if (
      url.match(/\/api\/v1\/domains\/[^/]+$/) &&
      method === "DELETE"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Single domain
    if (
      url.match(/\/api\/v1\/domains\/[^/]+$/) &&
      method === "GET"
    ) {
      const domainId = url.match(/\/domains\/([^/]+)$/)?.[1] ?? "";
      const domain = mockDomains.find((d) => d.id === domainId);
      return route.fulfill({
        status: domain ? 200 : 404,
        contentType: "application/json",
        body: JSON.stringify({ success: !!domain, data: domain }),
      });
    }

    // Domains list
    if (url.includes("/api/v1/domains") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: mockDomains,
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
            domainName: body.domainName,
            status: "PendingVerification",
            dnsRecords: [
              {
                type: "TXT",
                name: body.domainName,
                value: "v=spf1 include:amazonses.com ~all",
                purpose: "SPF",
                isVerified: false,
              },
            ],
            createdAt: new Date().toISOString(),
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
            totalCount: mockSuppressions.length,
            page: 1,
            pageSize: 20,
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
            emailAddress: body.email,
            reason: body.reason,
            suppressedAt: new Date().toISOString(),
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

    // ====== Admin Endpoints ======

    // Admin auth login
    if (url.includes("/api/v1/admin/auth/login") && method === "POST") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            userId: "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            email: "superadmin@eaas.io",
            displayName: "Super Admin",
            role: "superadmin",
            token: "mock-admin-jwt-token",
          },
        }),
      });
    }

    // Admin tenants - suspend
    if (
      url.match(/\/api\/v1\/admin\/tenants\/[^/]+\/suspend/) &&
      method === "POST"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Admin tenants - activate
    if (
      url.match(/\/api\/v1\/admin\/tenants\/[^/]+\/activate/) &&
      method === "POST"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Admin tenant - delete
    if (
      url.match(/\/api\/v1\/admin\/tenants\/[^/]+$/) &&
      method === "DELETE"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Admin tenant - single GET
    if (
      url.match(/\/api\/v1\/admin\/tenants\/[^/]+$/) &&
      method === "GET"
    ) {
      const tenantId =
        url.match(/\/admin\/tenants\/([^/?]+)/)?.[1] ?? "";
      const tenant = mockAdminTenants.find((t) => t.id === tenantId);
      return route.fulfill({
        status: tenant ? 200 : 404,
        contentType: "application/json",
        body: JSON.stringify({
          success: !!tenant,
          data: tenant
            ? {
                ...tenant,
                email: tenant.contactEmail,
                dailyEmailLimit: 1000,
                monthlyEmailLimit: 100000,
                maxApiKeys: 10,
                maxDomainsCount: 5,
                notes: null,
                updatedAt: tenant.createdAt,
              }
            : undefined,
        }),
      });
    }

    // Admin tenants - create
    if (url.includes("/api/v1/admin/tenants") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `tenant-${Date.now()}`,
            name: body.name,
            status: "active",
            ...body,
            apiKeyCount: 0,
            domainCount: 0,
            emailCount: 0,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
        }),
      });
    }

    // Admin tenants - list
    if (url.includes("/api/v1/admin/tenants") && method === "GET") {
      const u = new URL(url);
      let filtered = [...mockAdminTenants];
      const status = u.searchParams.get("status");
      if (status && status !== "all") {
        filtered = filtered.filter((t) => t.status === status);
      }
      const search = u.searchParams.get("search");
      if (search) {
        const q = search.toLowerCase();
        filtered = filtered.filter(
          (t) =>
            t.name.toLowerCase().includes(q) ||
            (t.companyName ?? "").toLowerCase().includes(q),
        );
      }
      const pg = parseInt(u.searchParams.get("page") ?? "1");
      const pageSize = parseInt(u.searchParams.get("page_size") ?? "20");
      const start = (pg - 1) * pageSize;
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: filtered.slice(start, start + pageSize),
            totalCount: filtered.length,
            page: pg,
            pageSize: pageSize,
            totalPages: Math.ceil(filtered.length / pageSize),
          },
        }),
      });
    }

    // Admin users - delete
    if (
      url.match(/\/api\/v1\/admin\/users\/[^/]+$/) &&
      method === "DELETE"
    ) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    }

    // Admin users - create
    if (url.includes("/api/v1/admin/users") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `admin-${Date.now()}`,
            ...body,
            isActive: true,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
        }),
      });
    }

    // Admin users - list
    if (url.includes("/api/v1/admin/users") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: mockAdminUsers,
            totalCount: mockAdminUsers.length,
            page: 1,
            pageSize: 20,
            totalPages: 1,
          },
        }),
      });
    }

    // Admin analytics - platform summary
    if (url.includes("/api/v1/admin/analytics/summary")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockPlatformSummary }),
      });
    }

    // Admin analytics - tenant rankings
    if (url.includes("/api/v1/admin/analytics/rankings")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: mockTenantRankings,
            totalCount: mockTenantRankings.length,
            page: 1,
            pageSize: 10,
          },
        }),
      });
    }

    // Admin analytics - growth metrics
    if (url.includes("/api/v1/admin/analytics/growth")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockGrowthMetrics }),
      });
    }

    // Admin system health
    if (url.includes("/api/v1/admin/health")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockAdminSystemHealth }),
      });
    }

    // Admin audit logs
    if (url.includes("/api/v1/admin/audit-logs") && method === "GET") {
      const u = new URL(url);
      let filtered = [...mockAuditLogs];
      const action = u.searchParams.get("action");
      if (action && action !== "all") {
        filtered = filtered.filter((l) => l.action === action);
      }
      const pg = parseInt(u.searchParams.get("page") ?? "1");
      const pageSize = parseInt(u.searchParams.get("page_size") ?? "20");
      const start = (pg - 1) * pageSize;
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: filtered.slice(start, start + pageSize),
            totalCount: filtered.length,
            page: pg,
            pageSize: pageSize,
            totalPages: Math.ceil(filtered.length / pageSize),
          },
        }),
      });
    }

    // Admin billing plans - create
    if (url.includes("/api/v1/admin/billing/plans") && method === "POST") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            id: `plan-${Date.now()}`,
            ...body,
            isActive: true,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
        }),
      });
    }

    // Admin billing plans - update
    if (url.match(/\/api\/v1\/admin\/billing\/plans\/[^/]+$/) && method === "PUT") {
      const body = JSON.parse(route.request().postData() ?? "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: { ...body, updatedAt: new Date().toISOString() },
        }),
      });
    }

    // Admin billing plans - list (paginated)
    if (url.includes("/api/v1/admin/billing/plans") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: {
            items: mockPlans,
            totalCount: mockPlans.length,
            page: 1,
            pageSize: 20,
          },
        }),
      });
    }

    // Billing - plans (customer-facing, flat array)
    if (url.includes("/api/v1/billing/plans") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockPlans }),
      });
    }

    // Billing - current subscription
    if (url.includes("/api/v1/billing/subscriptions/current") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockSubscription }),
      });
    }

    // Billing - invoices
    if (url.includes("/api/v1/billing/subscriptions/invoices") && method === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: mockInvoices }),
      });
    }

    // Billing - create subscription
    if (url.includes("/api/v1/billing/subscriptions") && method === "POST" && !url.includes("cancel")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          data: { ...mockSubscription, id: `sub-${Date.now()}` },
        }),
      });
    }

    // Billing - cancel subscription
    if (url.includes("/api/v1/billing/subscriptions/cancel") && method === "POST") {
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

/**
 * Mock all API proxy routes to return error responses.
 * Use in tests that verify error state rendering.
 */
export async function setupErrorMockApi(page: Page, statusCode: number = 500) {
  await page.route("**/api/proxy/**", async (route) => {
    return route.fulfill({
      status: statusCode,
      contentType: "application/json",
      body: JSON.stringify({ success: false, error: { message: "Server error" } }),
    });
  });
}

/**
 * Mock all API proxy GET routes to return empty lists.
 * Use in tests that verify empty state rendering.
 */
export async function setupEmptyMockApi(page: Page) {
  await page.route("**/api/proxy/**", async (route) => {
    if (route.request().method() === "GET") {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 20 } }),
      });
    }
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ success: true }) });
  });
}
