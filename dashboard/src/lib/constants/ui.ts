// ============================================================
// EaaS Dashboard - UI Constants
// ============================================================

export const PAGE_SIZE_DEFAULT = 20;
export const PAGE_SIZE_COMPACT = 10;
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
export const HEALTH_POLL_INTERVAL_MS = 30_000;
export const STALE_TIME_MS = 30_000;
export const DETAIL_STALE_TIME_MS = 5 * 60 * 1000;
export const REFETCH_INTERVAL_MS = 60_000;
export const MAX_ATTACHMENT_SIZE_MB = 30;
export const TOAST_DURATION_MS = 4_000;
export const TOOLTIP_DELAY_MS = 300;
export const DATA_TABLE_SKELETON_ROWS = 5;
export const STATS_SKELETON_COUNT = 6;

// ============================================================
// Chart Colors (hex values for Recharts — Tailwind classes won't work)
// ============================================================

/** Primary blue — sent, opened, volume lines */
export const CHART_COLOR_BLUE = "#2563eb";
/** Success green — delivered, processed */
export const CHART_COLOR_GREEN = "#22c55e";
/** Error red — bounced, failed */
export const CHART_COLOR_RED = "#ef4444";
/** Purple — clicked, admin accent */
export const CHART_COLOR_PURPLE = "#8b5cf6";
/** Admin brand purple */
export const CHART_COLOR_ADMIN = "#7c3aed";

/** Inbound analytics status colors */
export const INBOUND_STATUS_COLORS = {
  processed: "#34d399",
  failed: "#f87171",
  spam: "#fbbf24",
  virus: "#a78bfa",
} as const;

// ============================================================
// HTTP Status Code Badge Styles
// ============================================================

/** Returns badge className for an HTTP status code */
export function httpStatusCodeClass(code: number): string {
  if (code >= 200 && code < 300)
    return "bg-emerald-500/15 text-emerald-400 border-emerald-500/30";
  if (code >= 400 && code < 500)
    return "bg-amber-500/15 text-amber-400 border-amber-500/30";
  return "bg-red-500/15 text-red-400 border-red-500/30";
}

// ============================================================
// Email Event Timeline Colors (dot + icon class by status)
// ============================================================

export const EMAIL_EVENT_DOT_COLORS: Record<string, string> = {
  queued: "bg-gray-400",
  sending: "bg-blue-400",
  sent: "bg-blue-400",
  delivered: "bg-emerald-400",
  bounced: "bg-red-400",
  complained: "bg-amber-400",
  failed: "bg-red-500",
  opened: "bg-violet-400",
  clicked: "bg-cyan-400",
} as const;

export const EMAIL_EVENT_ICON_COLORS: Record<string, string> = {
  queued: "text-gray-400",
  sending: "text-blue-400",
  sent: "text-blue-400",
  delivered: "text-emerald-400",
  bounced: "text-red-400",
  complained: "text-amber-400",
  failed: "text-red-500",
  opened: "text-violet-400",
  clicked: "text-cyan-400",
} as const;
