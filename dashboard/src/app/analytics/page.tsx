"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { StatCard } from "@/components/overview/stat-card";
import {
  SendVolumeChart,
  DeliveryBreakdownChart,
  EngagementChart,
} from "@/components/analytics/charts";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Send,
  CheckCircle2,
  XCircle,
  Eye,
  MousePointerClick,
  AlertTriangle,
} from "lucide-react";
import { cn } from "@/lib/utils";

const dateRanges = [
  { label: "7d", days: 7 },
  { label: "30d", days: 30 },
  { label: "90d", days: 90 },
];

export default function AnalyticsPage() {
  const [selectedRange, setSelectedRange] = useState(30);

  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ["analytics", "summary", selectedRange],
    queryFn: () => api.getAnalyticsSummary(),
  });

  const { data: timeline, isLoading: timelineLoading } = useQuery({
    queryKey: ["analytics", "timeline", selectedRange],
    queryFn: () => api.getAnalyticsTimeline("day"),
  });

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Analytics</h1>
          <p className="text-sm text-white/50">
            Delivery performance, engagement tracking, and sending trends.
          </p>
        </div>
        <div className="flex items-center rounded-lg border border-white/10 bg-[#1E1E2E] p-1">
          {dateRanges.map((range) => (
            <Button
              key={range.days}
              variant="ghost"
              size="sm"
              onClick={() => setSelectedRange(range.days)}
              className={cn(
                "px-3 text-xs",
                selectedRange === range.days
                  ? "bg-[#7C4DFF] text-white hover:bg-[#7C4DFF]"
                  : "text-white/50 hover:text-white",
              )}
            >
              {range.label}
            </Button>
          ))}
        </div>
      </div>

      {/* KPI Cards */}
      {summaryLoading ? (
        <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-[120px] rounded-lg bg-white/5" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
          <StatCard
            title="Total Sent"
            value={summary?.total_sent.toLocaleString() ?? "0"}
            icon={Send}
            trend="up"
            trendValue="+12.5%"
            color="#7C4DFF"
          />
          <StatCard
            title="Delivered"
            value={summary?.delivered.toLocaleString() ?? "0"}
            subtitle={`${summary?.delivery_rate.toFixed(1)}%`}
            icon={CheckCircle2}
            trend="up"
            trendValue="+0.3%"
            color="#00E676"
          />
          <StatCard
            title="Bounced"
            value={summary?.bounced ?? 0}
            subtitle={`${summary?.bounce_rate.toFixed(2)}%`}
            icon={XCircle}
            trend="down"
            trendValue="-0.1%"
            color="#FF5252"
          />
          <StatCard
            title="Opened"
            value={summary?.opened.toLocaleString() ?? "0"}
            subtitle={`${summary?.open_rate.toFixed(1)}%`}
            icon={Eye}
            trend="up"
            trendValue="+2.1%"
            color="#7C4DFF"
          />
          <StatCard
            title="Clicked"
            value={summary?.clicked.toLocaleString() ?? "0"}
            subtitle={`${summary?.click_rate.toFixed(1)}%`}
            icon={MousePointerClick}
            trend="up"
            trendValue="+1.4%"
            color="#00E5FF"
          />
          <StatCard
            title="Complaints"
            value={summary?.complained ?? 0}
            subtitle={`${summary?.complaint_rate.toFixed(2)}% rate`}
            icon={AlertTriangle}
            trend="flat"
            trendValue="0.0%"
            color="#FFD740"
          />
        </div>
      )}

      {/* Charts */}
      {timelineLoading ? (
        <div className="grid gap-4 lg:grid-cols-2">
          <Skeleton className="h-[340px] rounded-lg bg-white/5" />
          <Skeleton className="h-[340px] rounded-lg bg-white/5" />
        </div>
      ) : (
        <>
          <div className="grid gap-4 lg:grid-cols-2">
            <SendVolumeChart data={timeline?.points ?? []} />
            <DeliveryBreakdownChart data={timeline?.points ?? []} />
          </div>
          <EngagementChart data={timeline?.points ?? []} />
        </>
      )}
    </div>
  );
}
