// ============================================================
// EaaS Dashboard - React Query Cache Key Factories
// ============================================================

export const QueryKeys = {
  emails: {
    all: ['emails'] as const,
    list: (params?: Record<string, unknown>) => ['emails', 'list', params] as const,
    detail: (id: string) => ['emails', 'detail', id] as const,
    events: (id: string) => ['emails', 'events', id] as const,
  },
  inboundEmails: {
    all: ['inbound-emails'] as const,
    list: (params?: Record<string, unknown>) => ['inbound-emails', 'list', params] as const,
    detail: (id: string) => ['inbound-emails', 'detail', id] as const,
    thread: (id: string) => ['inbound-emails', 'thread', id] as const,
  },
  inboundRules: {
    all: ['inbound-rules'] as const,
    list: (params?: Record<string, unknown>) => ['inbound-rules', 'list', params] as const,
    detail: (id: string) => ['inbound-rules', 'detail', id] as const,
  },
  templates: {
    all: ['templates'] as const,
    list: (params?: Record<string, unknown>) => ['templates', 'list', params] as const,
    detail: (id: string) => ['templates', 'detail', id] as const,
  },
  domains: {
    all: ['domains'] as const,
    list: () => ['domains', 'list'] as const,
    detail: (id: string) => ['domains', 'detail', id] as const,
  },
  apiKeys: {
    all: ['api-keys'] as const,
    list: () => ['api-keys', 'list'] as const,
  },
  webhooks: {
    all: ['webhooks'] as const,
    list: () => ['webhooks', 'list'] as const,
    detail: (id: string) => ['webhooks', 'detail', id] as const,
    deliveries: (id: string, params?: Record<string, unknown>) =>
      ['webhooks', 'deliveries', id, params] as const,
  },
  suppressions: {
    all: ['suppressions'] as const,
    list: (params?: Record<string, unknown>) => ['suppressions', 'list', params] as const,
  },
  analytics: {
    summary: (params?: Record<string, unknown>) => ['analytics', 'summary', params] as const,
    timeline: (params?: Record<string, unknown>) => ['analytics', 'timeline', params] as const,
    inboundSummary: (params?: Record<string, unknown>) =>
      ['analytics', 'inbound-summary', params] as const,
    inboundTimeline: (params?: Record<string, unknown>) =>
      ['analytics', 'inbound-timeline', params] as const,
    topSenders: (params?: Record<string, unknown>) =>
      ['analytics', 'top-senders', params] as const,
  },
  health: ['health'] as const,

  // Admin
  adminTenants: {
    all: ['admin-tenants'] as const,
    list: (params?: Record<string, unknown>) => ['admin-tenants', 'list', params] as const,
    detail: (id: string) => ['admin-tenants', 'detail', id] as const,
  },
  adminUsers: {
    all: ['admin-users'] as const,
    list: (params?: Record<string, unknown>) => ['admin-users', 'list', params] as const,
  },
  adminAnalytics: {
    platformSummary: ['admin-analytics', 'platform-summary'] as const,
    tenantRankings: (params?: Record<string, unknown>) =>
      ['admin-analytics', 'tenant-rankings', params] as const,
    growthMetrics: ['admin-analytics', 'growth-metrics'] as const,
  },
  adminHealth: ['admin-health'] as const,
  adminAuditLogs: {
    all: ['admin-audit-logs'] as const,
    list: (params?: Record<string, unknown>) => ['admin-audit-logs', 'list', params] as const,
  },

  adminBilling: {
    all: ['admin-billing'] as const,
    plans: (params?: Record<string, unknown>) => ['admin-billing', 'plans', params] as const,
  },

  billing: {
    plans: ['billing', 'plans'] as const,
    subscription: ['billing', 'subscription'] as const,
    invoices: (params?: Record<string, unknown>) => ['billing', 'invoices', params] as const,
  },
} as const;
