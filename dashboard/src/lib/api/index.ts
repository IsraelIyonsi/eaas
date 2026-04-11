// ============================================================
// EaaS Dashboard - Repository Instances
// ============================================================

import { EmailRepository } from './repositories/email.repository';
import { InboundEmailRepository } from './repositories/inbound-email.repository';
import { InboundRuleRepository } from './repositories/inbound-rule.repository';
import { TemplateRepository } from './repositories/template.repository';
import { DomainRepository } from './repositories/domain.repository';
import { ApiKeyRepository } from './repositories/api-key.repository';
import { WebhookRepository } from './repositories/webhook.repository';
import { SuppressionRepository } from './repositories/suppression.repository';
import { AnalyticsRepository } from './repositories/analytics.repository';
import { HealthRepository } from './repositories/health.repository';
import { AdminTenantRepository } from './repositories/admin-tenant.repository';
import { AdminUserRepository } from './repositories/admin-user.repository';
import { AdminAnalyticsRepository } from './repositories/admin-analytics.repository';
import { AdminHealthRepository } from './repositories/admin-health.repository';
import { AdminAuditLogRepository } from './repositories/admin-audit-log.repository';
import { AdminBillingRepository } from './repositories/admin-billing.repository';
import { BillingRepository } from './repositories/billing.repository';

export const repositories = {
  email: new EmailRepository(),
  inboundEmail: new InboundEmailRepository(),
  inboundRule: new InboundRuleRepository(),
  template: new TemplateRepository(),
  domain: new DomainRepository(),
  apiKey: new ApiKeyRepository(),
  webhook: new WebhookRepository(),
  suppression: new SuppressionRepository(),
  analytics: new AnalyticsRepository(),
  health: new HealthRepository(),
  adminTenant: new AdminTenantRepository(),
  adminUser: new AdminUserRepository(),
  adminAnalytics: new AdminAnalyticsRepository(),
  adminHealth: new AdminHealthRepository(),
  adminAuditLog: new AdminAuditLogRepository(),
  adminBilling: new AdminBillingRepository(),
  billing: new BillingRepository(),
} as const;

export { ApiError } from './client';
export type { HttpClient, CrudRepository } from './client';
