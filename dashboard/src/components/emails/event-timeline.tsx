"use client";

import { format, parseISO } from "date-fns";
import { cn } from "@/lib/utils";
import type { EmailEvent } from "@/types/email";
import type { EmailStatus } from "@/types/email";

const eventConfig: Record<
  EmailStatus,
  { color: string; label: string }
> = {
  queued: { color: "bg-gray-400", label: "Queued" },
  sending: { color: "bg-blue-400", label: "Sending" },
  sent: { color: "bg-blue-400", label: "Sent" },
  delivered: { color: "bg-emerald-400", label: "Delivered" },
  bounced: { color: "bg-red-400", label: "Bounced" },
  complained: { color: "bg-amber-400", label: "Complained" },
  failed: { color: "bg-red-500", label: "Failed" },
  opened: { color: "bg-violet-400", label: "Opened" },
  clicked: { color: "bg-cyan-400", label: "Clicked" },
};

interface EventTimelineProps {
  events: EmailEvent[];
}

export function EventTimeline({ events }: EventTimelineProps) {
  if (events.length === 0) {
    return (
      <p className="py-4 text-center text-sm text-muted-foreground/60">
        No events recorded yet
      </p>
    );
  }

  const sorted = [...events].sort(
    (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime(),
  );

  return (
    <div className="relative space-y-0">
      {sorted.map((event, idx) => {
        const config = eventConfig[event.eventType] ?? {
          color: "bg-gray-400",
          label: event.eventType,
        };
        const isLast = idx === sorted.length - 1;

        return (
          <div key={event.id} className="relative flex gap-4 pb-6">
            {/* Vertical line */}
            {!isLast && (
              <div className="absolute left-[7px] top-4 h-full w-px bg-muted" />
            )}

            {/* Dot */}
            <div className="relative z-10 mt-1 flex shrink-0">
              <span
                className={cn(
                  "inline-block h-[14px] w-[14px] rounded-full border-2 border-card",
                  config.color,
                )}
              />
            </div>

            {/* Content */}
            <div className="min-w-0 flex-1">
              <div className="flex items-baseline gap-2">
                <span className="text-sm font-medium text-foreground">
                  {config.label}
                </span>
                <span className="text-xs text-muted-foreground/60">
                  {format(parseISO(event.timestamp), "MMM d, yyyy HH:mm:ss")}
                </span>
              </div>
              {event.details && (
                <p className="mt-0.5 text-xs text-muted-foreground">{event.details}</p>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
