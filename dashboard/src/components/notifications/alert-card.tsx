"use client";

import { Card, CardContent } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import { formatDistanceToNow, parseISO } from "date-fns";

interface AlertCardProps {
  title: string;
  description: string;
  enabled: boolean;
  onToggle: (enabled: boolean) => void;
  children: React.ReactNode;
  lastTriggered?: string;
}

export function AlertCard({
  title,
  description,
  enabled,
  onToggle,
  children,
  lastTriggered,
}: AlertCardProps) {
  return (
    <Card className="border-border bg-card shadow-none">
      <CardContent className="p-5 space-y-4">
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h3 className="text-sm font-semibold text-foreground">{title}</h3>
            <p className="text-xs text-muted-foreground">{description}</p>
          </div>
          <Switch checked={enabled} onCheckedChange={onToggle} />
        </div>

        <div
          className={cn(
            "space-y-3 transition-opacity",
            !enabled && "pointer-events-none opacity-40",
          )}
        >
          {children}
        </div>

        {lastTriggered && (
          <p className="text-[11px] text-muted-foreground/40">
            Last triggered{" "}
            {formatDistanceToNow(parseISO(lastTriggered), { addSuffix: true })}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
