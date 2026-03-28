// ============================================================
// EaaS Dashboard - TypeScript Interfaces
// ============================================================

// --- API Envelope ---
export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  page_size: number;
  total_pages: number;
}

// --- Email ---
export type EmailStatus =
  | "queued"
  | "sending"
  | "delivered"
  | "bounced"
  | "complained"
  | "failed"
  | "opened"
  | "clicked";

export interface Email {
  id: string;
  message_id: string;
  from: string;
  to: string;
  cc?: string[];
  bcc?: string[];
  subject: string;
  status: EmailStatus;
  template_id?: string;
  template_name?: string;
  tags?: string[];
  html_body?: string;
  text_body?: string;
  created_at: string;
  sent_at?: string;
  delivered_at?: string;
  opened_at?: string;
  clicked_at?: string;
}

export interface EmailEvent {
  id: string;
  email_id: string;
  event_type: EmailStatus;
  timestamp: string;
  details?: string;
}

// --- Template ---
export interface Template {
  id: string;
  name: string;
  subject: string;
  html_body: string;
  text_body: string;
  variables_schema?: string;
  version: number;
  created_at: string;
  updated_at: string;
  deleted_at?: string;
}

// --- Domain ---
export type DomainStatus =
  | "pending_verification"
  | "verified"
  | "failed"
  | "suspended";

export interface DnsRecord {
  type: string;
  name: string;
  value: string;
  status: "verified" | "missing" | "mismatch";
}

export interface Domain {
  id: string;
  domain: string;
  status: DomainStatus;
  dns_records: DnsRecord[];
  verified_at?: string;
  created_at: string;
}

// --- Analytics ---
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
  granularity: "hour" | "day";
  points: TimelinePoint[];
}

// --- Suppression ---
export type SuppressionReason =
  | "hard_bounce"
  | "soft_bounce_limit"
  | "complaint"
  | "manual";

export interface Suppression {
  id: string;
  email: string;
  reason: SuppressionReason;
  created_at: string;
}

// --- System Health ---
export type HealthStatus = "healthy" | "degraded" | "down";

export interface ServiceHealth {
  name: string;
  status: HealthStatus;
  latency_ms?: number;
}

export interface SystemHealth {
  status: HealthStatus;
  services: ServiceHealth[];
}
