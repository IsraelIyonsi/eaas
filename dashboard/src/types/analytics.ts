// ============================================================
// EaaS Dashboard - Analytics Types
// ============================================================

export interface AnalyticsSummary {
  total_sent: number;
  delivered: number;
  bounced: number;
  complained: number;
  opened: number;
  clicked: number;
  failed: number;
  delivery_rate: number;
  open_rate: number;
  click_rate: number;
  bounce_rate: number;
  complaint_rate: number;
}

export interface TimelinePoint {
  timestamp: string;
  sent: number;
  delivered: number;
  bounced: number;
  complained: number;
  opened: number;
  clicked: number;
}

export interface AnalyticsTimeline {
  granularity: 'hour' | 'day';
  points: TimelinePoint[];
}

export interface InboundAnalyticsSummary {
  total_received: number;
  processed: number;
  forwarded: number;
  failed: number;
  spam_flagged: number;
  virus_flagged: number;
  avg_processing_time_ms: number;
  processing_rate: number;
}

export interface TopSender {
  email: string;
  name?: string;
  total_emails: number;
  last_receivedAt: string;
}
