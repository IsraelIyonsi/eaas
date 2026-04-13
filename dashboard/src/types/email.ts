// ============================================================
// EaaS Dashboard - Email Types
// ============================================================

import type { PaginationParams, SortParams, DateRangeParams } from './common';

export type EmailStatus =
  | 'queued'
  | 'sending'
  | 'sent'
  | 'delivered'
  | 'bounced'
  | 'complained'
  | 'failed'
  | 'opened'
  | 'clicked'
  | 'scheduled';

export interface Email {
  id: string;
  messageId: string;
  from: string;
  to: string | string[];
  cc?: string[];
  bcc?: string[];
  subject: string;
  status: EmailStatus;
  templateId?: string;
  templateName?: string;
  tags?: string[];
  htmlBody?: string;
  textBody?: string;
  createdAt: string;
  sentAt?: string;
  deliveredAt?: string;
  openedAt?: string;
  clickedAt?: string;
}

export interface EmailEvent {
  id: string;
  emailId?: string;
  eventType: EmailStatus;
  /** API returns createdAt; normalized to timestamp for display */
  timestamp: string;
  createdAt?: string;
  details?: string;
  data?: string;
}

export interface EmailListParams extends PaginationParams, SortParams, DateRangeParams {
  status?: EmailStatus;
  search?: string;
  templateId?: string;
  tags?: string;
}
