"use client";

import { format, parseISO } from "date-fns";
import { cn } from "@/lib/utils";
import type { EmailEvent } from "@/types/email";
import type { EmailStatus } from "@/types/email";
import { EMAIL_EVENT_DOT_COLORS } from "@/lib/constants/ui";
import { EmailStatusConfig } from "@/lib/constants/status";

const eventConfig: Record<
  EmailStatus,
  { color: string; label: string }
> = Object.fromEntries(
  Object.entries(EmailStatusConfig).map(([key, { label }]) => [
    key,
    { color: EMAIL_EVENT_DOT_COLORS[key] ?? "bg-gray-400", label },
  ]),
) as Record<EmailStatus, { color: string; label: string }>;

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
