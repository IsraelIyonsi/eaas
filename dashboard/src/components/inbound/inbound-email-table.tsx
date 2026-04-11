"use client";

import Link from "next/link";
import { Paperclip } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { InboundEmailStatusConfig } from "@/lib/constants/status";
import { Routes } from "@/lib/constants/routes";
import type { InboundEmail, InboundEmailStatus } from "@/types/inbound";

function formatRelativeTime(dateString: string): string {
  const now = Date.now();
  const date = new Date(dateString).getTime();
  const diff = now - date;
  const seconds = Math.floor(diff / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function StatusDot({ status }: { status: InboundEmailStatus }) {
  const config = InboundEmailStatusConfig[status];
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className={cn("inline-block h-2 w-2 rounded-full", config.color)} />
      <span className={cn("text-xs font-medium", config.textColor)}>
        {config.label}
      </span>
    </span>
  );
}

function AvatarCircle({ name }: { name: string }) {
  const letter = (name || "?")[0].toUpperCase();
  return (
    <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/20 text-xs font-semibold text-primary">
      {letter}
    </div>
  );
}

function WebhookStatusBadge({ status }: { status?: string }) {
  const resolved = status ?? "pending";
  const styles: Record<string, string> = {
    delivered: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
    failed: "bg-red-500/15 text-red-400 border-red-500/30",
    pending: "bg-amber-500/15 text-amber-400 border-amber-500/30",
  };
  const labels: Record<string, string> = {
    delivered: "Delivered",
    failed: "Failed",
    pending: "Pending",
  };
  return (
    <Badge
      variant="outline"
      className={cn("text-xs font-medium", styles[resolved] ?? styles.pending)}
    >
      {labels[resolved] ?? "Pending"}
    </Badge>
  );
}

export function getInboundEmailColumns() {
  return [
    {
      key: "status",
      header: "Status",
      className: "w-[120px]",
      render: (email: InboundEmail) => <StatusDot status={email.status} />,
    },
    {
      key: "from",
      header: "From",
      render: (email: InboundEmail) => (
        <div className="flex items-center gap-2">
          <AvatarCircle name={email.fromName || email.fromEmail} />
          <div className="min-w-0">
            {email.fromName && (
              <div className="truncate text-sm text-foreground">
                {email.fromName}
              </div>
            )}
            <div className="truncate text-xs text-muted-foreground">
              {email.fromEmail}
            </div>
          </div>
        </div>
      ),
    },
    {
      key: "to",
      header: "To",
      render: (email: InboundEmail) => (
        <span className="text-sm text-foreground/80">
          {email.toEmails[0]?.email ?? "-"}
          {email.toEmails.length > 1 && (
            <span className="text-muted-foreground/60">
              {" "}+{email.toEmails.length - 1}
            </span>
          )}
        </span>
      ),
    },
    {
      key: "subject",
      header: "Subject",
      render: (email: InboundEmail) => (
        <Link
          href={Routes.INBOUND_EMAIL_DETAIL(email.id)}
          className="block max-w-[250px] truncate text-sm text-foreground hover:underline"
        >
          {email.subject || "(no subject)"}
        </Link>
      ),
    },
    {
      key: "receivedAt",
      header: "Received",
      className: "w-[100px]",
      render: (email: InboundEmail) => (
        <span className="text-xs text-muted-foreground">
          {formatRelativeTime(email.receivedAt)}
        </span>
      ),
    },
    {
      key: "attachments",
      header: "Attachments",
      className: "w-[100px]",
      render: (email: InboundEmail) => {
        const count = (email as unknown as { attachmentCount?: number }).attachmentCount ?? email.attachments?.length ?? 0;
        return count > 0 ? (
          <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
            <Paperclip className="h-3 w-3" />
            {count}
          </span>
        ) : null;
      },
    },
    {
      key: "webhook_status",
      header: "Webhook",
      className: "w-[100px]",
      render: (email: InboundEmail) => {
        // Derive webhook status from email status
        const webhookStatus =
          email.status === "processed" || email.status === "forwarded"
            ? "delivered"
            : email.status === "failed"
              ? "failed"
              : "pending";
        return <WebhookStatusBadge status={webhookStatus} />;
      },
    },
  ];
}
