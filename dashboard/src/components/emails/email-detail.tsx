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
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import type { EmailStatus } from "@/types";

const eventIconMap: Record<EmailStatus, { icon: LucideIcon; color: string }> = {
  queued: { icon: Clock, color: "text-gray-400" },
  sending: { icon: Send, color: "text-blue-400" },
  delivered: { icon: CheckCircle2, color: "text-emerald-400" },
  bounced: { icon: XCircle, color: "text-red-400" },
  complained: { icon: AlertTriangle, color: "text-amber-400" },
  failed: { icon: XCircle, color: "text-red-500" },
  opened: { icon: Eye, color: "text-violet-400" },
  clicked: { icon: MousePointerClick, color: "text-cyan-400" },
};

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
        className="w-full border-white/10 bg-[#1E1E2E] sm:max-w-lg overflow-y-auto"
      >
        <SheetHeader className="pb-4">
          <SheetTitle className="text-left text-lg font-bold text-white">
            Email Detail
          </SheetTitle>
        </SheetHeader>

        {/* Metadata */}
        <div className="space-y-3 pb-4">
          <MetaRow label="Status">
            <EmailStatusBadge status={email.status} />
          </MetaRow>
          <MetaRow label="Message ID">
            <code className="text-xs text-[#00E5FF]">{email.message_id}</code>
          </MetaRow>
          <MetaRow label="From">
            <span className="text-sm text-white/80">{email.from}</span>
          </MetaRow>
          <MetaRow label="To">
            <span className="text-sm text-white/80">{email.to}</span>
          </MetaRow>
          {email.cc && email.cc.length > 0 && (
            <MetaRow label="CC">
              <span className="text-sm text-white/70">
                {email.cc.join(", ")}
              </span>
            </MetaRow>
          )}
          <MetaRow label="Subject">
            <span className="text-sm font-medium text-white">{email.subject}</span>
          </MetaRow>
          {email.template_name && (
            <MetaRow label="Template">
              <span className="text-sm text-[#7C4DFF]">
                {email.template_name}
              </span>
            </MetaRow>
          )}
          {email.tags && email.tags.length > 0 && (
            <MetaRow label="Tags">
              <div className="flex gap-1">
                {email.tags.map((tag) => (
                  <span
                    key={tag}
                    className="rounded bg-white/10 px-1.5 py-0.5 text-xs text-white/60"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </MetaRow>
          )}
        </div>

        <Separator className="bg-white/10" />

        {/* Event Timeline */}
        <div className="py-4">
          <h3 className="mb-4 text-xs font-semibold uppercase tracking-wider text-white/40">
            Event Timeline
          </h3>
          <div className="relative space-y-0">
            {events.map((evt, i) => {
              const config = eventIconMap[evt.event_type] ?? {
                icon: CircleDot,
                color: "text-white/40",
              };
              const Icon = config.icon;
              return (
                <div key={evt.id} className="flex gap-3">
                  <div className="flex flex-col items-center">
                    <div
                      className={cn(
                        "flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-white/5",
                        config.color,
                      )}
                    >
                      <Icon className="h-3.5 w-3.5" />
                    </div>
                    {i < events.length - 1 && (
                      <div className="h-8 w-px bg-white/10" />
                    )}
                  </div>
                  <div className="pb-6">
                    <p className="text-sm font-medium capitalize text-white/80">
                      {evt.event_type.replace("_", " ")}
                    </p>
                    <p className="text-xs text-white/40">
                      {format(parseISO(evt.timestamp), "MMM d, yyyy HH:mm:ss")}
                    </p>
                    {evt.details && (
                      <p className="mt-0.5 text-xs text-white/30">
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
        {email.html_body && (
          <>
            <Separator className="bg-white/10" />
            <div className="py-4">
              <h3 className="mb-3 text-xs font-semibold uppercase tracking-wider text-white/40">
                Body Preview
              </h3>
              <div className="rounded-lg border border-white/10 bg-white overflow-hidden">
                <iframe
                  srcDoc={email.html_body}
                  title="Email preview"
                  className="h-[200px] w-full"
                  sandbox=""
                />
              </div>
            </div>
          </>
        )}
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
      <span className="w-24 shrink-0 text-xs font-medium uppercase tracking-wider text-white/40">
        {label}
      </span>
      <div className="min-w-0">{children}</div>
    </div>
  );
}
