// ============================================================
// EaaS Dashboard - Types Barrel Export
// ============================================================

// Common
export type { ApiResponse, PaginatedResponse, PaginationParams, SortParams, DateRangeParams } from './common';

// Email
export type { Email, EmailEvent, EmailListParams } from './email';
export type { EmailStatus } from './email';

// Inbound
export type {
  InboundEmail,
  InboundAttachment,
  InboundRule,
  CreateInboundRuleRequest,
  UpdateInboundRuleRequest,
  InboundEmailListParams,
  WebhookDeliveryLog,
} from './inbound';
export type { InboundEmailStatus, InboundRuleAction, VerdictStatus } from './inbound';

// Template
export type { Template, CreateTemplateRequest, UpdateTemplateRequest } from './template';

// Domain
export type { Domain, DnsRecord, DomainDetail } from './domain';
export type { DomainStatus } from './domain';

// API Key
export type { ApiKey, CreateApiKeyRequest, CreateApiKeyResponse } from './api-key';

// Webhook
export type {
  Webhook,
  WebhookDelivery,
  CreateWebhookRequest,
  UpdateWebhookRequest,
  TestWebhookResult,
} from './webhook';
export type { WebhookStatus } from './webhook';
export { WEBHOOK_EVENT_TYPES } from './webhook';

// Suppression
export type { Suppression, CreateSuppressionRequest } from './suppression';
export type { SuppressionReason } from './suppression';

// Analytics
export type {
  AnalyticsSummary,
  TimelinePoint,
  AnalyticsTimeline,
  InboundAnalyticsSummary,
  TopSender,
} from './analytics';

// Health
export type { ServiceHealth, SystemHealth } from './health';
export type { HealthStatus } from './health';

// Admin
export type {
  AdminTenant,
  AdminUser,
  AuditLog,
  PlatformSummary,
  TenantRanking,
  GrowthMetrics,
  AdminSystemHealth,
  AdminServiceHealth,
  AdminHealthMetrics,
  AdminTenantListParams,
  AdminUserListParams,
  AuditLogListParams,
  CreateAdminUserRequest,
  UpdateTenantRequest,
} from './admin';
export type { TenantStatus, AdminRole } from './admin';
