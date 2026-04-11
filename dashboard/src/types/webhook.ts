// ============================================================
// EaaS Dashboard - Webhook Types
// ============================================================

export type WebhookStatus = 'active' | 'inactive' | 'disabled';

export interface Webhook {
  id: string;
  url: string;
  events: string[];
  secret?: string;
  status: WebhookStatus;
  createdAt: string;
  updatedAt: string;
}

export interface WebhookDelivery {
  id: string;
  webhook_id: string;
  eventType: string;
  statusCode: number;
  responseTimeMs: number;
  payload_sizeBytes: number;
  timestamp: string;
  error?: string;
}

export interface CreateWebhookRequest {
  url: string;
  events: string[];
  secret?: string;
}

export interface UpdateWebhookRequest {
  url?: string;
  events?: string[];
  secret?: string;
  status?: WebhookStatus;
}

export interface TestWebhookResult {
  success: boolean;
  statusCode: number;
  responseTimeMs: number;
  error?: string;
}

export const WEBHOOK_EVENT_TYPES = [
  'email.queued',
  'email.sent',
  'email.delivered',
  'email.bounced',
  'email.complained',
  'email.opened',
  'email.clicked',
  'email.failed',
  'inbound.received',
] as const;
