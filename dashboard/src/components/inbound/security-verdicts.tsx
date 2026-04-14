"use client";

import { CheckCircle2, XCircle, HelpCircle } from "lucide-react";
import { cn } from "@/lib/utils";
import type { VerdictStatus } from "@/types/inbound";

interface SecurityVerdictsProps {
  spf?: VerdictStatus;
  dkim?: VerdictStatus;
  dmarc?: VerdictStatus;
  spam?: VerdictStatus;
  virus?: VerdictStatus;
}

const verdictIcon = {
  pass: CheckCircle2,
  fail: XCircle,
  unknown: HelpCircle,
} as const;

const verdictColor = {
  pass: "text-emerald-400",
  fail: "text-red-400",
  unknown: "text-muted-foreground/40",
} as const;

const verdictBg = {
  pass: "bg-emerald-500/10 border-emerald-500/20",
  fail: "bg-red-500/10 border-red-500/20",
  unknown: "bg-muted border-border",
} as const;

function VerdictBadge({
  label,
  status,
}: {
  label: string;
  status?: VerdictStatus;
}) {
  const resolved = (status?.toLowerCase() as VerdictStatus) ?? "unknown";
  const Icon = verdictIcon[resolved];

  return (
    <div
      className={cn(
        "inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1",
        verdictBg[resolved],
      )}
    >
      <Icon className={cn("h-3.5 w-3.5", verdictColor[resolved])} />
      <span className={cn("text-xs font-medium", verdictColor[resolved])}>
        {label}
      </span>
    </div>
  );
}

export function SecurityVerdicts({
  spf,
  dkim,
  dmarc,
  spam,
  virus,
}: SecurityVerdictsProps) {
  return (
    <div className="flex flex-wrap gap-2">
      <VerdictBadge label="SPF" status={spf} />
      <VerdictBadge label="DKIM" status={dkim} />
      <VerdictBadge label="DMARC" status={dmarc} />
      <VerdictBadge label="Spam" status={spam} />
      <VerdictBadge label="Virus" status={virus} />
    </div>
  );
}
