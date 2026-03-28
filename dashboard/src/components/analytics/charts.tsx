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
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { TimelinePoint } from "@/types";
import { format, parseISO } from "date-fns";

// Custom tooltip matching dark theme
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
    <div className="rounded-lg border border-white/10 bg-[#1E1E2E] px-3 py-2 shadow-xl">
      <p className="mb-1 text-xs text-white/50">{label}</p>
      {payload.map((p) => (
        <div key={p.name} className="flex items-center gap-2 text-xs">
          <span
            className="inline-block h-2 w-2 rounded-full"
            style={{ backgroundColor: p.color }}
          />
          <span className="text-white/70">{p.name}:</span>
          <span className="font-semibold text-white">
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
  const formatted = data.map((p) => ({
    ...p,
    date: format(parseISO(p.timestamp), "MMM d"),
  }));

  return (
    <Card className="border-white/10 bg-[#1E1E2E] shadow-none">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold text-white">
          Email Volume
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-[280px]">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={formatted}>
              <defs>
                <linearGradient id="sentGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#7C4DFF" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#7C4DFF" stopOpacity={0} />
                </linearGradient>
                <linearGradient
                  id="deliveredGrad"
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop offset="5%" stopColor="#00E676" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#00E676" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="rgba(255,255,255,0.06)"
              />
              <XAxis
                dataKey="date"
                tick={{ fill: "rgba(255,255,255,0.4)", fontSize: 11 }}
                axisLine={{ stroke: "rgba(255,255,255,0.1)" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fill: "rgba(255,255,255,0.4)", fontSize: 11 }}
                axisLine={{ stroke: "rgba(255,255,255,0.1)" }}
                tickLine={false}
              />
              <RechartsTooltip content={<CustomTooltip />} />
              <Legend
                wrapperStyle={{ fontSize: 12, color: "rgba(255,255,255,0.6)" }}
              />
              <Area
                type="monotone"
                dataKey="sent"
                name="Sent"
                stroke="#7C4DFF"
                fill="url(#sentGrad)"
                strokeWidth={2}
              />
              <Area
                type="monotone"
                dataKey="delivered"
                name="Delivered"
                stroke="#00E676"
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
  const formatted = data.map((p) => ({
    ...p,
    date: format(parseISO(p.timestamp), "MMM d"),
  }));

  return (
    <Card className="border-white/10 bg-[#1E1E2E] shadow-none">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold text-white">
          Delivery vs Bounce
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-[280px]">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={formatted}>
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="rgba(255,255,255,0.06)"
              />
              <XAxis
                dataKey="date"
                tick={{ fill: "rgba(255,255,255,0.4)", fontSize: 11 }}
                axisLine={{ stroke: "rgba(255,255,255,0.1)" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fill: "rgba(255,255,255,0.4)", fontSize: 11 }}
                axisLine={{ stroke: "rgba(255,255,255,0.1)" }}
                tickLine={false}
              />
              <RechartsTooltip content={<CustomTooltip />} />
              <Legend
                wrapperStyle={{ fontSize: 12, color: "rgba(255,255,255,0.6)" }}
              />
              <Bar
                dataKey="delivered"
                name="Delivered"
                fill="#00E676"
                radius={[3, 3, 0, 0]}
                opacity={0.85}
              />
              <Bar
                dataKey="bounced"
                name="Bounced"
                fill="#FF5252"
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
  const formatted = data.map((p) => ({
    ...p,
    date: format(parseISO(p.timestamp), "MMM d"),
  }));

  return (
    <Card className="border-white/10 bg-[#1E1E2E] shadow-none">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold text-white">
          Engagement Trends
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-[280px]">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={formatted}>
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="rgba(255,255,255,0.06)"
              />
              <XAxis
                dataKey="date"
                tick={{ fill: "rgba(255,255,255,0.4)", fontSize: 11 }}
                axisLine={{ stroke: "rgba(255,255,255,0.1)" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fill: "rgba(255,255,255,0.4)", fontSize: 11 }}
                axisLine={{ stroke: "rgba(255,255,255,0.1)" }}
                tickLine={false}
              />
              <RechartsTooltip content={<CustomTooltip />} />
              <Legend
                wrapperStyle={{ fontSize: 12, color: "rgba(255,255,255,0.6)" }}
              />
              <Line
                type="monotone"
                dataKey="opened"
                name="Opened"
                stroke="#7C4DFF"
                strokeWidth={2}
                dot={false}
              />
              <Line
                type="monotone"
                dataKey="clicked"
                name="Clicked"
                stroke="#00E5FF"
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
