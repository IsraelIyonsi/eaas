"use client";

import { Card, CardContent } from "@/components/ui/card";
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
    <Card className="border-white/10 bg-[#1E1E2E] shadow-none">
      <CardContent className="p-5">
        <div className="flex items-start justify-between">
          <div className="space-y-2">
            <div className="flex items-center gap-1.5">
              <p className="text-xs font-medium uppercase tracking-wider text-white/50">
                {title}
              </p>
              {tooltip && (
                <Tooltip>
                  <TooltipTrigger className="inline-flex cursor-help">
                    <Info className="h-3 w-3 text-white/30" />
                  </TooltipTrigger>
                  <TooltipContent side="top" className="max-w-xs text-xs">
                    {tooltip}
                  </TooltipContent>
                </Tooltip>
              )}
            </div>
            <p className="text-2xl font-bold text-white">{value}</p>
            {subtitle && (
              <p className="text-xs text-white/40">{subtitle}</p>
            )}
          </div>
          <div
            className="flex h-10 w-10 items-center justify-center rounded-lg"
            style={{ backgroundColor: `${color}15` }}
          >
            <Icon className="h-5 w-5" style={{ color }} />
          </div>
        </div>
        {trend && trendValue && (
          <div className="mt-3 flex items-center gap-1">
            <TrendIcon
              className={cn(
                "h-3 w-3",
                trend === "up" && "text-emerald-400",
                trend === "down" && "text-red-400",
                trend === "flat" && "text-white/40",
              )}
            />
            <span
              className={cn(
                "text-xs font-medium",
                trend === "up" && "text-emerald-400",
                trend === "down" && "text-red-400",
                trend === "flat" && "text-white/40",
              )}
            >
              {trendValue}
            </span>
            <span className="text-xs text-white/30">vs last period</span>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
