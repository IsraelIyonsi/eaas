"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { StatCard } from "@/components/overview/stat-card";
import { HealthStatus } from "@/components/overview/health-status";
import { SendVolumeChart } from "@/components/analytics/charts";
import { EmailTable } from "@/components/emails/email-table";
import { EmailDetailSheet } from "@/components/emails/email-detail";
import {
  Send,
  CheckCircle2,
  XCircle,
  Eye,
  MousePointerClick,
  AlertTriangle,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useState } from "react";
import type { Email } from "@/types";

export default function OverviewPage() {
  const [selectedEmail, setSelectedEmail] = useState<Email | null>(null);

  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ["analytics", "summary"],
    queryFn: () => api.getAnalyticsSummary(),
  });

  const { data: timeline, isLoading: timelineLoading } = useQuery({
    queryKey: ["analytics", "timeline"],
    queryFn: () => api.getAnalyticsTimeline("day"),
  });

  const { data: emails, isLoading: emailsLoading } = useQuery({
    queryKey: ["emails", "recent"],
    queryFn: () => api.getEmails({ page: 1, page_size: 10 }),
  });

  const { data: health } = useQuery({
    queryKey: ["health"],
    queryFn: () => api.getHealth(),
    refetchInterval: 30000,
  });

  const { data: events } = useQuery({
    queryKey: ["email-events", selectedEmail?.id],
    queryFn: () =>
      selectedEmail ? api.getEmailEvents(selectedEmail) : Promise.resolve([]),
    enabled: !!selectedEmail,
  });

  if (summaryLoading) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-xl font-bold text-white">Overview</h1>
          <p className="text-sm text-white/50">
            System health and email sending activity at a glance.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton
              key={i}
              className="h-[120px] rounded-lg bg-white/5"
            />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-xl font-bold text-white">Overview</h1>
        <p className="text-sm text-white/50">
          System health and email sending activity at a glance.
        </p>
      </div>

      {/* Stat Cards */}
      <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
        <StatCard
          title="Sent Today"
          value={summary?.total_sent.toLocaleString() ?? "0"}
          icon={Send}
          trend="up"
          trendValue="+12.5%"
          color="#7C4DFF"
          tooltip="Total emails accepted by the API and enqueued for delivery in the selected period."
        />
        <StatCard
          title="Delivery Rate"
          value={`${summary?.delivery_rate.toFixed(1) ?? "0"}%`}
          icon={CheckCircle2}
          trend="up"
          trendValue="+0.3%"
          color="#00E676"
          tooltip="Percentage of sent emails confirmed delivered by the recipient's mail server."
        />
        <StatCard
          title="Bounce Rate"
          value={`${summary?.bounce_rate.toFixed(2) ?? "0"}%`}
          icon={XCircle}
          trend="down"
          trendValue="-0.1%"
          color="#FF5252"
          tooltip="Percentage of sent emails permanently rejected. Keep below 2% to protect sender reputation."
        />
        <StatCard
          title="Open Rate"
          value={`${summary?.open_rate.toFixed(1) ?? "0"}%`}
          icon={Eye}
          trend="up"
          trendValue="+2.1%"
          color="#7C4DFF"
          tooltip="Percentage of delivered emails where the tracking pixel was loaded. Undercounts due to image blocking."
        />
        <StatCard
          title="Click Rate"
          value={`${summary?.click_rate.toFixed(1) ?? "0"}%`}
          icon={MousePointerClick}
          trend="up"
          trendValue="+1.4%"
          color="#00E5FF"
          tooltip="Percentage of delivered emails where at least one tracked link was clicked."
        />
        <StatCard
          title="Complaints"
          value={summary?.complained ?? 0}
          subtitle={`${summary?.complaint_rate.toFixed(2) ?? "0"}% rate`}
          icon={AlertTriangle}
          trend="flat"
          trendValue="0.0%"
          color="#FFD740"
          tooltip="Percentage of delivered emails marked as spam by recipients. SES suspends at 0.1%."
        />
      </div>

      {/* Charts + Health Row */}
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          {timelineLoading ? (
            <Skeleton className="h-[340px] rounded-lg bg-white/5" />
          ) : (
            <SendVolumeChart data={timeline?.points ?? []} />
          )}
        </div>
        <div>{health && <HealthStatus health={health} />}</div>
      </div>

      {/* Recent Emails */}
      <Card className="border-white/10 bg-[#1E1E2E] shadow-none">
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-semibold text-white">
            Recent Emails
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {emailsLoading ? (
            <div className="p-6">
              <Skeleton className="h-[200px] bg-white/5" />
            </div>
          ) : (
            <EmailTable
              emails={emails?.items ?? []}
              total={emails?.total ?? 0}
              page={1}
              pageSize={10}
              totalPages={1}
              onPageChange={() => {}}
              onRowClick={setSelectedEmail}
              compact
            />
          )}
        </CardContent>
      </Card>

      {/* Email Detail Sheet */}
      <EmailDetailSheet
        email={selectedEmail}
        events={events ?? []}
        open={!!selectedEmail}
        onClose={() => setSelectedEmail(null)}
      />
    </div>
  );
}
