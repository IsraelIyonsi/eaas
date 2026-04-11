// ============================================================
// EaaS Dashboard - Suppression Types
// ============================================================

export type SuppressionReason =
  | 'hard_bounce'
  | 'soft_bounce_limit'
  | 'complaint'
  | 'manual';

export interface Suppression {
  id: string;
  emailAddress: string;
  reason: SuppressionReason;
  sourceMessageId?: string | null;
  suppressedAt: string;
}

export interface CreateSuppressionRequest {
  email: string;
  reason: SuppressionReason;
}
