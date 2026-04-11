"use client";

import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { TrendingUp, TrendingDown, Minus, Info, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface StatCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  icon: LucideIcon;
  trend?: "up" | "down" | "flat";
  trendValue?: string;
  color: string;
  tooltip?: string;
}

export function StatCard({
  title,
  value,
  subtitle,
  icon: Icon,
  trend,
  trendValue,
  color,
  tooltip,
}: StatCardProps) {
  const TrendIcon =
    trend === "up" ? TrendingUp : trend === "down" ? TrendingDown : Minus;

  return (
    <div className="metric-card">
      <div className="flex items-start justify-between">
        <div>
          {/* Label: 12px, font-weight 500, uppercase, tracking 0.04em, text-secondary, with icon */}
          <div className="metric-label">
            <span>{title}</span>
            {tooltip && (
              <Tooltip>
                <TooltipTrigger className="inline-flex cursor-help">
                  <Info className="h-3 w-3 text-muted-foreground/50" />
                </TooltipTrigger>
                <TooltipContent side="top" className="max-w-xs text-xs">
                  {tooltip}
                </TooltipContent>
              </Tooltip>
            )}
          </div>

          {/* Value: 28px, font-weight 700, text-primary, tracking -0.03em, line-height 1 */}
          <p className="metric-value">{value}</p>

          {/* Subtitle: 12px, text-tertiary */}
          {subtitle && (
            <p className="mt-1 text-xs text-muted-foreground/70">{subtitle}</p>
          )}
        </div>

        {/* Icon badge */}
        <div
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg"
          style={{ backgroundColor: `${color}15` }}
        >
          <Icon className="h-5 w-5" style={{ color }} />
        </div>
      </div>

      {/* Change: 12px, font-weight 500, with up/down arrow, green/red */}
      {trend && trendValue && (
        <div className="metric-change">
          <TrendIcon
            className={cn(
              "h-3 w-3",
              trend === "up" && "text-emerald-500",
              trend === "down" && "text-red-500",
              trend === "flat" && "text-muted-foreground",
            )}
          />
          <span
            className={cn(
              "text-xs font-medium",
              trend === "up" && "text-emerald-500",
              trend === "down" && "text-red-500",
              trend === "flat" && "text-muted-foreground",
            )}
          >
            {trendValue}
          </span>
          <span className="text-xs text-muted-foreground/70">vs last period</span>
        </div>
      )}
    </div>
  );
}
