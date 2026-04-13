// ============================================================
// EaaS Dashboard - Status Enums & Display Metadata
// ============================================================

export const EmailStatusConfig = {
  queued: { label: 'Queued', color: 'bg-gray-500', textColor: 'text-gray-400' },
  sending: { label: 'Sending', color: 'bg-blue-500', textColor: 'text-blue-400' },
  sent: { label: 'Sent', color: 'bg-blue-500', textColor: 'text-blue-400' },
  delivered: { label: 'Delivered', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  bounced: { label: 'Bounced', color: 'bg-red-500', textColor: 'text-red-400' },
  complained: { label: 'Complained', color: 'bg-amber-500', textColor: 'text-amber-400' },
  failed: { label: 'Failed', color: 'bg-red-500', textColor: 'text-red-400' },
  opened: { label: 'Opened', color: 'bg-purple-500', textColor: 'text-purple-400' },
  clicked: { label: 'Clicked', color: 'bg-cyan-500', textColor: 'text-cyan-400' },
  scheduled: { label: 'Scheduled', color: 'bg-indigo-500', textColor: 'text-indigo-400' },
} as const;

export const InboundEmailStatusConfig = {
  received: { label: 'Received', color: 'bg-blue-500', textColor: 'text-blue-400' },
  processing: { label: 'Processing', color: 'bg-amber-500', textColor: 'text-amber-400' },
  processed: { label: 'Processed', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  forwarded: { label: 'Forwarded', color: 'bg-purple-500', textColor: 'text-purple-400' },
  failed: { label: 'Failed', color: 'bg-red-500', textColor: 'text-red-400' },
} as const;

export const DomainStatusConfig = {
  PendingVerification: { label: 'Pending', color: 'bg-amber-500', textColor: 'text-amber-400' },
  Verified: { label: 'Verified', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  Failed: { label: 'Failed', color: 'bg-red-500', textColor: 'text-red-400' },
  Suspended: { label: 'Suspended', color: 'bg-red-600', textColor: 'text-red-500' },
} as const;

export const ApiKeyStatusConfig = {
  active: { label: 'Active', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  inactive: { label: 'Inactive', color: 'bg-red-500', textColor: 'text-red-400' },
} as const;

export const WebhookStatusConfig = {
  active: { label: 'Active', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  inactive: { label: 'Inactive', color: 'bg-gray-500', textColor: 'text-gray-400' },
  disabled: { label: 'Disabled', color: 'bg-red-500', textColor: 'text-red-400' },
} as const;

export const SuppressionReasonConfig = {
  hard_bounce: { label: 'Hard Bounce', color: 'bg-red-500', textColor: 'text-red-400' },
  soft_bounce_limit: { label: 'Soft Bounce Limit', color: 'bg-amber-500', textColor: 'text-amber-400' },
  complaint: { label: 'Complaint', color: 'bg-orange-500', textColor: 'text-orange-400' },
  manual: { label: 'Manual', color: 'bg-gray-500', textColor: 'text-gray-400' },
} as const;

export const InboundRuleActionConfig = {
  webhook: { label: 'Webhook', color: 'bg-blue-500', textColor: 'text-blue-400' },
  forward: { label: 'Forward', color: 'bg-purple-500', textColor: 'text-purple-400' },
  store: { label: 'Store', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
} as const;

export const VerdictStatusConfig = {
  pass: { label: 'Pass', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  fail: { label: 'Fail', color: 'bg-red-500', textColor: 'text-red-400' },
  unknown: { label: 'Unknown', color: 'bg-gray-500', textColor: 'text-gray-400' },
} as const;

export const HealthStatusConfig = {
  healthy: { label: 'Healthy', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  degraded: { label: 'Degraded', color: 'bg-amber-500', textColor: 'text-amber-400' },
  down: { label: 'Down', color: 'bg-red-500', textColor: 'text-red-400' },
} as const;

export const DnsRecordStatusConfig = {
  true: { label: 'Verified', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  false: { label: 'Not Verified', color: 'bg-red-500', textColor: 'text-red-400' },
} as const;

export const TenantStatusConfig = {
  active: { label: 'Active', color: 'bg-emerald-500', textColor: 'text-emerald-400' },
  suspended: { label: 'Suspended', color: 'bg-red-500', textColor: 'text-red-400' },
  deactivated: { label: 'Deactivated', color: 'bg-gray-500', textColor: 'text-gray-400' },
} as const;

export const AdminRoleConfig = {
  superadmin: { label: 'Super Admin', color: 'bg-purple-500', textColor: 'text-purple-400' },
  admin: { label: 'Admin', color: 'bg-blue-500', textColor: 'text-blue-400' },
  readonly: { label: 'Read Only', color: 'bg-gray-500', textColor: 'text-gray-400' },
} as const;
