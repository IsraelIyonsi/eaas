"use client";

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import { EmailStatusBadge } from "@/components/shared/status-badge";
import type { Email, EmailEvent } from "@/types";
import { format, parseISO } from "date-fns";
import {
  Clock,
  Send,
  CheckCircle2,
  Eye,
  MousePointerClick,
  XCircle,
  AlertTriangle,
  CircleDot,
  CalendarClock,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import type { EmailStatus } from "@/types";
import { EMAIL_EVENT_ICON_COLORS } from "@/lib/constants/ui";

const EVENT_ICONS: Record<EmailStatus, LucideIcon> = {
  queued: Clock,
  sending: Send,
  sent: Send,
  delivered: CheckCircle2,
  bounced: XCircle,
  complained: AlertTriangle,
  failed: XCircle,
  opened: Eye,
  clicked: MousePointerClick,
  scheduled: CalendarClock,
};

const eventIconMap: Record<EmailStatus, { icon: LucideIcon; color: string }> =
  Object.fromEntries(
    Object.entries(EVENT_ICONS).map(([key, icon]) => [
      key,
      { icon, color: EMAIL_EVENT_ICON_COLORS[key] ?? "text-muted-foreground" },
    ]),
  ) as Record<EmailStatus, { icon: LucideIcon; color: string }>;

interface EmailDetailProps {
  email: Email | null;
  events: EmailEvent[];
  open: boolean;
  onClose: () => void;
}

export function EmailDetailSheet({
  email,
  events,
  open,
  onClose,
}: EmailDetailProps) {
  if (!email) return null;

  return (
    <Sheet open={open} onOpenChange={onClose}>
      <SheetContent
        side="right"
        className="w-full border-border bg-card sm:max-w-lg overflow-y-auto"
      >
        <div className="px-6 py-6">
        <SheetHeader className="pb-6">
          <SheetTitle className="text-left text-lg font-bold text-foreground">
            Email Detail
          </SheetTitle>
        </SheetHeader>

        {/* Metadata */}
        <div className="space-y-4 pb-6">
          <MetaRow label="Status">
            <EmailStatusBadge status={email.status} />
          </MetaRow>
          <MetaRow label="Message ID">
            <code className="text-xs text-[var(--chart-1)]">{email.messageId}</code>
          </MetaRow>
          <MetaRow label="From">
            <span className="text-sm text-foreground">{email.from}</span>
          </MetaRow>
          <MetaRow label="To">
            <span className="text-sm text-foreground">{Array.isArray(email.to) ? email.to.join(", ") : email.to}</span>
          </MetaRow>
          {email.cc && email.cc.length > 0 && (
            <MetaRow label="CC">
              <span className="text-sm text-foreground/80">
                {email.cc.join(", ")}
              </span>
            </MetaRow>
          )}
          <MetaRow label="Subject">
            <span className="text-sm font-medium text-foreground">{email.subject}</span>
          </MetaRow>
          {email.templateName && (
            <MetaRow label="Template">
              <span className="text-sm text-primary">
                {email.templateName}
              </span>
            </MetaRow>
          )}
          {email.tags && email.tags.length > 0 && (
            <MetaRow label="Tags">
              <div className="flex gap-1">
                {email.tags.map((tag) => (
                  <span
                    key={tag}
                    className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </MetaRow>
          )}
        </div>

        <Separator className="bg-muted" />

        {/* Event Timeline */}
        <div className="py-6">
          <h3 className="mb-4 text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
            Event Timeline
          </h3>
          <div className="relative ml-3 border-l-2 border-border pl-6 space-y-6">
            {events.map((evt, i) => {
              const config = eventIconMap[evt.eventType] ?? {
                icon: CircleDot,
                color: "text-muted-foreground/60",
              };
              const Icon = config.icon;
              return (
                <div key={evt.id} className="relative">
                  {/* Timeline dot */}
                  <div
                    className={cn(
                      "absolute -left-[calc(1.5rem+0.5625rem)] flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-card ring-2 ring-white/10",
                      config.color,
                    )}
                  >
                    <Icon className="h-3.5 w-3.5" />
                  </div>
                  <div>
                    <p className="text-sm font-medium capitalize text-foreground">
                      {evt.eventType.replace("_", " ")}
                    </p>
                    <p className="mt-0.5 text-xs text-muted-foreground/60">
                      {format(parseISO(evt.timestamp), "MMM d, yyyy HH:mm:ss")}
                    </p>
                    {evt.details && (
                      <p className="mt-1 text-xs text-muted-foreground/40">
                        {evt.details}
                      </p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* HTML Preview */}
        {email.htmlBody && (
          <>
            <Separator className="bg-muted" />
            <div className="py-6">
              <h3 className="mb-4 text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                Body Preview
              </h3>
              <div className="rounded-lg border border-border bg-white overflow-hidden">
                <iframe
                  srcDoc={email.htmlBody}
                  title="Email preview"
                  className="h-[200px] w-full"
                  sandbox=""
                />
              </div>
            </div>
          </>
        )}
        </div>
      </SheetContent>
    </Sheet>
  );
}

function MetaRow({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-start gap-3">
      <span className="w-24 shrink-0 text-xs font-medium uppercase tracking-wider text-muted-foreground/60">
        {label}
      </span>
      <div className="min-w-0">{children}</div>
    </div>
  );
}
