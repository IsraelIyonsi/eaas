// ============================================================
// EaaS Dashboard - Inbound Email Types
// ============================================================

import type { PaginationParams, DateRangeParams } from './common';

export type InboundEmailStatus = 'received' | 'processing' | 'processed' | 'forwarded' | 'failed';
export type InboundRuleAction = 'webhook' | 'forward' | 'store';
/**
 * Normalized verdict status used throughout the UI.
 *
 * The API returns raw SES verdict strings (`PASS`, `FAIL`, `GRAY`,
 * `PROCESSING_FAILED`, `DISABLED`, ...). Normalization to this union
 * happens at the API boundary in `InboundEmailRepository` — anything
 * outside pass/fail collapses to `unknown`.
 */
export type VerdictStatus = 'pass' | 'fail' | 'unknown';

export interface InboundEmail {
  id: string;
  messageId: string;
  fromEmail: string;
  fromName?: string;
  toEmails: Array<{ email: string; name?: string }>;
  ccEmails: Array<{ email: string; name?: string }>;
  replyTo?: string;
  subject?: string;
  htmlBody?: string;
  textBody?: string;
  headers?: Record<string, string>;
  status: InboundEmailStatus;
  s3Key?: string;
  spamVerdict?: VerdictStatus;
  virusVerdict?: VerdictStatus;
  spfVerdict?: VerdictStatus;
  dkimVerdict?: VerdictStatus;
  dmarcVerdict?: VerdictStatus;
  inReplyTo?: string;
  outboundEmailId?: string;
  attachments: InboundAttachment[];
  receivedAt: string;
  processedAt?: string;
  createdAt: string;
}

export interface InboundAttachment {
  id: string;
  filename: string;
  contentType: string;
  sizeBytes: number;
  s3Key: string;
  contentId?: string;
  isInline: boolean;
}

export interface InboundRule {
  id: string;
  name: string;
  domainId: string;
  domainName: string;
  matchPattern: string;
  action: InboundRuleAction;
  webhookUrl?: string;
  forwardTo?: string;
  isActive: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateInboundRuleRequest {
  name: string;
  domainId: string;
  matchPattern: string;
  action: string;
  webhookUrl?: string;
  forwardTo?: string;
  priority: number;
}

export interface UpdateInboundRuleRequest {
  name?: string;
  matchPattern?: string;
  action?: string;
  webhookUrl?: string;
  forwardTo?: string;
  isActive?: boolean;
  priority?: number;
}

export interface InboundEmailListParams extends PaginationParams, DateRangeParams {
  status?: InboundEmailStatus;
  from?: string;
  to?: string;
  has_attachments?: boolean;
}

export interface WebhookDeliveryLog {
  id: string;
  attempt: number;
  statusCode: number;
  responseTimeMs: number;
  timestamp: string;
  error?: string;
}
