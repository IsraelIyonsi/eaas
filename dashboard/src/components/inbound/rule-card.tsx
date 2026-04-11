"use client";

import { Pencil, Trash2, ArrowRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import { InboundRuleActionConfig } from "@/lib/constants/status";
import type { InboundRule } from "@/types/inbound";

interface RuleCardProps {
  rule: InboundRule;
  onEdit: (rule: InboundRule) => void;
  onDelete: (rule: InboundRule) => void;
  onToggle: (rule: InboundRule) => void;
}

export function RuleCard({ rule, onEdit, onDelete, onToggle }: RuleCardProps) {
  const actionConfig = InboundRuleActionConfig[rule.action];

  return (
    <div className="rounded-lg border border-border bg-card p-4 transition-colors hover:border-border">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1 space-y-3">
          {/* Priority + Name */}
          <div className="flex items-center gap-2">
            <Badge
              variant="outline"
              className="border-[var(--primary)]/30 bg-primary/10 text-xs font-mono text-primary"
            >
              #{rule.priority}
            </Badge>
            <span className="truncate text-sm font-medium text-foreground">
              {rule.name}
            </span>
          </div>

          {/* Domain */}
          <div className="text-xs text-muted-foreground">
            Domain: <span className="text-foreground/80">{rule.domainName}</span>
          </div>

          {/* Pattern -> Action */}
          <div className="flex items-center gap-2 text-xs">
            <code className="rounded bg-muted px-1.5 py-0.5 text-amber-400">
              {rule.matchPattern}
            </code>
            <ArrowRight className="h-3 w-3 text-muted-foreground/40" />
            <Badge
              variant="outline"
              className={cn(
                "text-xs font-medium",
                `${actionConfig.color}/15 ${actionConfig.textColor} border-transparent`,
              )}
            >
              {actionConfig.label}
            </Badge>
          </div>

          {/* Webhook URL or Forward To */}
          {rule.action === "webhook" && rule.webhookUrl && (
            <div className="truncate text-xs text-muted-foreground/60">
              {rule.webhookUrl}
            </div>
          )}
          {rule.action === "forward" && rule.forwardTo && (
            <div className="truncate text-xs text-muted-foreground/60">
              {rule.forwardTo}
            </div>
          )}
        </div>

        {/* Right side: toggle + actions */}
        <div className="flex shrink-0 flex-col items-end gap-3">
          <Switch
            checked={rule.isActive}
            onCheckedChange={() => onToggle(rule)}
            size="sm"
          />
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onEdit(rule)}
              className="h-7 w-7 p-0 text-muted-foreground/60 hover:text-foreground"
            >
              <Pencil className="h-3.5 w-3.5" />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onDelete(rule)}
              className="h-7 w-7 p-0 text-muted-foreground/60 hover:text-red-400"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
