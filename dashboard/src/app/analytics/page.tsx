"use client";

import { useState } from "react";
import { useAnalyticsSummary, useAnalyticsTimeline } from "@/lib/hooks/use-analytics";
import { PageHeader } from "@/components/shared/page-header";
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

  const { data: summary, isLoading: summaryLoading } = useAnalyticsSummary();
  const { data: timeline, isLoading: timelineLoading } = useAnalyticsTimeline({ granularity: 'day' });

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        title="Analytics"
        description="Delivery performance, engagement tracking, and sending trends."
        action={
          <div className="flex items-center rounded-lg border border-border bg-card p-1">
            {dateRanges.map((range) => (
              <Button
                key={range.days}
                variant="ghost"
                size="sm"
                onClick={() => setSelectedRange(range.days)}
                className={cn(
                  "px-3 text-xs",
                  selectedRange === range.days
                    ? "bg-primary text-primary-foreground hover:bg-primary/90"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                {range.label}
              </Button>
            ))}
          </div>
        }
      />

      {/* KPI Cards */}
      {summaryLoading ? (
        <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-[120px] rounded-lg bg-muted" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
          <StatCard
            title="Total Sent"
            value={summary?.total_sent.toLocaleString() ?? "0"}
            icon={Send}
            color="var(--primary)"
          />
          <StatCard
            title="Delivered"
            value={summary?.delivered.toLocaleString() ?? "0"}
            subtitle={`${summary?.delivery_rate.toFixed(1)}%`}
            icon={CheckCircle2}
            color="var(--chart-2)"
          />
          <StatCard
            title="Bounced"
            value={summary?.bounced ?? 0}
            subtitle={`${summary?.bounce_rate.toFixed(2)}%`}
            icon={XCircle}
            color="var(--destructive)"
          />
          <StatCard
            title="Opened"
            value={summary?.opened.toLocaleString() ?? "0"}
            subtitle={`${summary?.open_rate.toFixed(1)}%`}
            icon={Eye}
            color="var(--primary)"
          />
          <StatCard
            title="Clicked"
            value={summary?.clicked.toLocaleString() ?? "0"}
            subtitle={`${summary?.click_rate.toFixed(1)}%`}
            icon={MousePointerClick}
            color="var(--chart-1)"
          />
          <StatCard
            title="Complaints"
            value={summary?.complained ?? 0}
            subtitle={`${summary?.complaint_rate.toFixed(2)}% rate`}
            icon={AlertTriangle}
            color="var(--chart-3)"
          />
        </div>
      )}

      {/* Charts */}
      {timelineLoading ? (
        <div className="grid gap-4 lg:grid-cols-2">
          <Skeleton className="h-[340px] rounded-lg bg-muted" />
          <Skeleton className="h-[340px] rounded-lg bg-muted" />
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
