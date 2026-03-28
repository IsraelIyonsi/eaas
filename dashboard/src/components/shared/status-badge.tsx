"use client";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { EmailStatus, DomainStatus, HealthStatus } from "@/types";

const emailStatusConfig: Record<
  EmailStatus,
  { label: string; className: string }
> = {
  queued: {
    label: "Queued",
    className: "bg-gray-500/15 text-gray-400 border-gray-500/30",
  },
  sending: {
    label: "Sending",
    className: "bg-blue-500/15 text-blue-400 border-blue-500/30",
  },
  delivered: {
    label: "Delivered",
    className: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
  },
  bounced: {
    label: "Bounced",
    className: "bg-red-500/15 text-red-400 border-red-500/30",
  },
  complained: {
    label: "Complained",
    className: "bg-amber-500/15 text-amber-400 border-amber-500/30",
  },
  failed: {
    label: "Failed",
    className: "bg-red-600/15 text-red-500 border-red-600/30",
  },
  opened: {
    label: "Opened",
    className: "bg-violet-500/15 text-violet-400 border-violet-500/30",
  },
  clicked: {
    label: "Clicked",
    className: "bg-cyan-500/15 text-cyan-400 border-cyan-500/30",
  },
};

const domainStatusConfig: Record<
  DomainStatus,
  { label: string; className: string }
> = {
  pending_verification: {
    label: "Pending",
    className: "bg-amber-500/15 text-amber-400 border-amber-500/30",
  },
  verified: {
    label: "Verified",
    className: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
  },
  failed: {
    label: "Failed",
    className: "bg-red-500/15 text-red-400 border-red-500/30",
  },
  suspended: {
    label: "Suspended",
    className: "bg-red-600/15 text-red-500 border-red-600/30",
  },
};

const healthStatusConfig: Record<
  HealthStatus,
  { label: string; className: string; dotColor: string }
> = {
  healthy: {
    label: "Healthy",
    className: "text-emerald-400",
    dotColor: "bg-emerald-400",
  },
  degraded: {
    label: "Degraded",
    className: "text-amber-400",
    dotColor: "bg-amber-400",
  },
  down: {
    label: "Down",
    className: "text-red-400",
    dotColor: "bg-red-400",
  },
};

export function EmailStatusBadge({ status }: { status: EmailStatus }) {
  const config = emailStatusConfig[status];
  return (
    <Badge
      variant="outline"
      className={cn("text-xs font-medium", config.className)}
    >
      {config.label}
    </Badge>
  );
}

export function DomainStatusBadge({ status }: { status: DomainStatus }) {
  const config = domainStatusConfig[status];
  return (
    <Badge
      variant="outline"
      className={cn("text-xs font-medium", config.className)}
    >
      {config.label}
    </Badge>
  );
}

export function HealthDot({ status }: { status: HealthStatus }) {
  const config = healthStatusConfig[status];
  return (
    <span className="inline-flex items-center gap-1.5">
      <span
        className={cn(
          "inline-block h-2 w-2 rounded-full",
          config.dotColor,
          status === "healthy" && "animate-pulse",
        )}
      />
      <span className={cn("text-xs font-medium", config.className)}>
        {config.label}
      </span>
    </span>
  );
}

export function SuppressionReasonBadge({
  reason,
}: {
  reason: string;
}) {
  const labels: Record<string, { label: string; className: string }> = {
    hard_bounce: {
      label: "Hard Bounce",
      className: "bg-red-500/15 text-red-400 border-red-500/30",
    },
    soft_bounce_limit: {
      label: "Soft Bounce Limit",
      className: "bg-amber-500/15 text-amber-400 border-amber-500/30",
    },
    complaint: {
      label: "Complaint",
      className: "bg-orange-500/15 text-orange-400 border-orange-500/30",
    },
    manual: {
      label: "Manual",
      className: "bg-gray-500/15 text-gray-400 border-gray-500/30",
    },
  };
  const config = labels[reason] ?? labels.manual;
  return (
    <Badge
      variant="outline"
      className={cn("text-xs font-medium", config.className)}
    >
      {config.label}
    </Badge>
  );
}
