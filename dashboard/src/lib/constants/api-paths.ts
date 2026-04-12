// ============================================================
// EaaS Dashboard - Backend API Endpoint Paths
// ============================================================

export const ApiPaths = {
  // Emails
  EMAILS: '/api/v1/emails',
  EMAIL_BY_ID: (id: string) => `/api/v1/emails/${id}`,
  EMAIL_EVENTS: (id: string) => `/api/v1/emails/${id}/events`,
  EMAIL_BATCH: '/api/v1/emails/batch',

  // Inbound Emails
  INBOUND_EMAILS: '/api/v1/inbound/emails',
  INBOUND_EMAIL_BY_ID: (id: string) => `/api/v1/inbound/emails/${id}`,
  INBOUND_EMAIL_RAW: (id: string) => `/api/v1/inbound/emails/${id}/raw`,
  INBOUND_EMAIL_ATTACHMENT: (emailId: string, attachmentId: string) =>
    `/api/v1/inbound/emails/${emailId}/attachments/${attachmentId}`,
  INBOUND_EMAIL_RETRY_WEBHOOK: (id: string) => `/api/v1/inbound/emails/${id}/retry-webhook`,

  // Inbound Rules
  INBOUND_RULES: '/api/v1/inbound/rules',
  INBOUND_RULE_BY_ID: (id: string) => `/api/v1/inbound/rules/${id}`,

  // Templates
  TEMPLATES: '/api/v1/templates',
  TEMPLATE_BY_ID: (id: string) => `/api/v1/templates/${id}`,
  TEMPLATE_PREVIEW: (id: string) => `/api/v1/templates/${id}/preview`,
  TEMPLATE_VERSIONS: (id: string) => `/api/v1/templates/${id}/versions`,
  TEMPLATE_ROLLBACK: (id: string) => `/api/v1/templates/${id}/rollback`,

  // Domains
  DOMAINS: '/api/v1/domains',
  DOMAIN_BY_ID: (id: string) => `/api/v1/domains/${id}`,
  DOMAIN_VERIFY: (id: string) => `/api/v1/domains/${id}/verify`,

  // API Keys
  API_KEYS: '/api/v1/keys',
  API_KEY_REVOKE: (id: string) => `/api/v1/keys/${id}/revoke`,
  API_KEY_ROTATE: (id: string) => `/api/v1/keys/${id}/rotate`,

  // Webhooks
  WEBHOOKS: '/api/v1/webhooks',
  WEBHOOK_BY_ID: (id: string) => `/api/v1/webhooks/${id}`,
  WEBHOOK_TEST: (id: string) => `/api/v1/webhooks/${id}/test`,
  WEBHOOK_DELIVERIES: (id: string) => `/api/v1/webhooks/${id}/deliveries`,

  // Suppressions
  SUPPRESSIONS: '/api/v1/suppressions',
  SUPPRESSION_BY_ID: (id: string) => `/api/v1/suppressions/${id}`,

  // Analytics
  ANALYTICS_SUMMARY: '/api/v1/analytics/summary',
  ANALYTICS_TIMELINE: '/api/v1/analytics/timeline',
  ANALYTICS_INBOUND_SUMMARY: '/api/v1/analytics/inbound/summary',
  ANALYTICS_INBOUND_TIMELINE: '/api/v1/analytics/inbound/timeline',
  ANALYTICS_TOP_SENDERS: '/api/v1/analytics/inbound/top-senders',

  // Health
  HEALTH: '/health',

  // Admin - Tenants
  ADMIN_TENANTS: '/api/v1/admin/tenants',
  ADMIN_TENANT_BY_ID: (id: string) => `/api/v1/admin/tenants/${id}`,
  ADMIN_TENANT_SUSPEND: (id: string) => `/api/v1/admin/tenants/${id}/suspend`,
  ADMIN_TENANT_ACTIVATE: (id: string) => `/api/v1/admin/tenants/${id}/activate`,

  // Admin - Users
  ADMIN_USERS: '/api/v1/admin/users',
  ADMIN_USER_BY_ID: (id: string) => `/api/v1/admin/users/${id}`,

  // Admin - Analytics
  ADMIN_PLATFORM_SUMMARY: '/api/v1/admin/analytics/summary',
  ADMIN_TENANT_RANKINGS: '/api/v1/admin/analytics/rankings',
  ADMIN_GROWTH_METRICS: '/api/v1/admin/analytics/growth',

  // Admin - Health
  ADMIN_SYSTEM_HEALTH: '/api/v1/admin/health',

  // Admin - Audit Logs
  ADMIN_AUDIT_LOGS: '/api/v1/admin/audit-logs',

  // Admin - Billing Plans
  ADMIN_BILLING_PLANS: '/api/v1/admin/billing/plans',
  ADMIN_BILLING_PLAN_BY_ID: (id: string) => `/api/v1/admin/billing/plans/${id}`,

  // Billing
  BILLING_PLANS: '/api/v1/billing/plans',
  BILLING_SUBSCRIPTION: '/api/v1/billing/subscriptions/current',
  BILLING_SUBSCRIBE: '/api/v1/billing/subscriptions',
  BILLING_CANCEL: '/api/v1/billing/subscriptions/cancel',
  BILLING_INVOICES: '/api/v1/billing/subscriptions/invoices',
} as const;
