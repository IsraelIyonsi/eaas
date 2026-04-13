"use client";

import {
  LineChart,
  Line,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  ResponsiveContainer,
  Legend,
  Area,
  AreaChart,
} from "recharts";
import { useMemo } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { TimelinePoint } from "@/types";
import { format, parseISO } from "date-fns";
import {
  CHART_COLOR_BLUE,
  CHART_COLOR_GREEN,
  CHART_COLOR_RED,
  CHART_COLOR_PURPLE,
} from "@/lib/constants/ui";

// Custom tooltip matching theme
function CustomTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: Array<{ name: string; value: number; color: string }>;
  label?: string;
}) {
  if (!active || !payload) return null;
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 shadow-xl">
      <p className="mb-1 text-xs text-muted-foreground">{label}</p>
      {payload.map((p) => (
        <div key={p.name} className="flex items-center gap-2 text-xs">
          <span
            className="inline-block h-2 w-2 rounded-full"
            style={{ backgroundColor: p.color }}
          />
          <span className="text-muted-foreground">{p.name}:</span>
          <span className="font-semibold text-foreground">
            {p.value.toLocaleString()}
          </span>
        </div>
      ))}
    </div>
  );
}

interface SendVolumeChartProps {
  data: TimelinePoint[];
}

export function SendVolumeChart({ data }: SendVolumeChartProps) {
  const formatted = useMemo(
    () =>
      data.map((p) => ({
        ...p,
        date: format(parseISO(p.timestamp), "MMM d"),
      })),
    [data],
  );

  return (
    <Card className="border-border bg-card shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold text-foreground">
          Email Volume
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-[280px]">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={formatted}>
              <defs>
                <linearGradient id="sentGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={CHART_COLOR_BLUE} stopOpacity={0.3} />
                  <stop offset="95%" stopColor={CHART_COLOR_BLUE} stopOpacity={0} />
                </linearGradient>
                <linearGradient
                  id="deliveredGrad"
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop offset="5%" stopColor={CHART_COLOR_GREEN} stopOpacity={0.3} />
                  <stop offset="95%" stopColor={CHART_COLOR_GREEN} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="var(--border)"
                opacity={0.5}
              />
              <XAxis
                dataKey="date"
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <RechartsTooltip content={<CustomTooltip />} />
              <Legend
                wrapperStyle={{ fontSize: 12, color: "var(--muted-foreground)" }}
              />
              <Area
                type="monotone"
                dataKey="sent"
                name="Sent"
                stroke={CHART_COLOR_BLUE}
                fill="url(#sentGrad)"
                strokeWidth={2}
              />
              <Area
                type="monotone"
                dataKey="delivered"
                name="Delivered"
                stroke={CHART_COLOR_GREEN}
                fill="url(#deliveredGrad)"
                strokeWidth={2}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  );
}

export function DeliveryBreakdownChart({ data }: SendVolumeChartProps) {
  const formatted = useMemo(
    () =>
      data.map((p) => ({
        ...p,
        date: format(parseISO(p.timestamp), "MMM d"),
      })),
    [data],
  );

  return (
    <Card className="border-border bg-card shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold text-foreground">
          Delivery vs Bounce
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-[280px]">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={formatted}>
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="var(--border)"
                opacity={0.5}
              />
              <XAxis
                dataKey="date"
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <RechartsTooltip content={<CustomTooltip />} />
              <Legend
                wrapperStyle={{ fontSize: 12, color: "var(--muted-foreground)" }}
              />
              <Bar
                dataKey="delivered"
                name="Delivered"
                fill={CHART_COLOR_GREEN}
                radius={[3, 3, 0, 0]}
                opacity={0.85}
              />
              <Bar
                dataKey="bounced"
                name="Bounced"
                fill={CHART_COLOR_RED}
                radius={[3, 3, 0, 0]}
                opacity={0.85}
              />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  );
}

export function EngagementChart({ data }: SendVolumeChartProps) {
  const formatted = useMemo(
    () =>
      data.map((p) => ({
        ...p,
        date: format(parseISO(p.timestamp), "MMM d"),
      })),
    [data],
  );

  return (
    <Card className="border-border bg-card shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold text-foreground">
          Engagement Trends
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-[280px]">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={formatted}>
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="var(--border)"
                opacity={0.5}
              />
              <XAxis
                dataKey="date"
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <RechartsTooltip content={<CustomTooltip />} />
              <Legend
                wrapperStyle={{ fontSize: 12, color: "var(--muted-foreground)" }}
              />
              <Line
                type="monotone"
                dataKey="opened"
                name="Opened"
                stroke={CHART_COLOR_BLUE}
                strokeWidth={2}
                dot={false}
              />
              <Line
                type="monotone"
                dataKey="clicked"
                name="Clicked"
                stroke={CHART_COLOR_PURPLE}
                strokeWidth={2}
                dot={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  );
}
