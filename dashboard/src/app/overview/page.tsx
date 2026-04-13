"use client";

import { extractItems } from "@/lib/utils/api-response";

import { useEmails, useEmailEvents } from "@/lib/hooks/use-emails";
import { useAnalyticsSummary, useAnalyticsTimeline } from "@/lib/hooks/use-analytics";
import { useHealth } from "@/lib/hooks/use-health";
import { PageHeader } from "@/components/shared/page-header";
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
import { Skeleton } from "@/components/ui/skeleton";
import { PAGE_SIZE_COMPACT, STATS_SKELETON_COUNT } from "@/lib/constants/ui";
import { useState } from "react";
import type { Email } from "@/types";

export default function OverviewPage() {
  const [selectedEmail, setSelectedEmail] = useState<Email | null>(null);

  const { data: summary, isLoading: summaryLoading } = useAnalyticsSummary();
  const { data: timeline, isLoading: timelineLoading } = useAnalyticsTimeline({ granularity: 'day' });
  const { data: emails, isLoading: emailsLoading } = useEmails({ page: 1, page_size: PAGE_SIZE_COMPACT });
  const { data: health } = useHealth();
  const { data: events } = useEmailEvents(selectedEmail?.id);

  if (summaryLoading) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Overview"
          description="System health and email sending activity at a glance."
        />
        {/* Hi-fi: 3 columns, 16px gap */}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: STATS_SKELETON_COUNT }).map((_, i) => (
            <Skeleton
              key={i}
              className="h-[120px] rounded-lg bg-muted"
            />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        title="Overview"
        description="System health and email sending activity at a glance."
      />

      {/* Stat Cards — hi-fi: grid 3 columns, 16px gap (2 rows of 3) */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard
          title="Sent Today"
          value={summary?.total_sent.toLocaleString() ?? "0"}
          icon={Send}
          color="var(--primary)"
          tooltip="Total emails accepted by the API and enqueued for delivery in the selected period."
        />
        <StatCard
          title="Delivery Rate"
          value={`${summary?.delivery_rate.toFixed(1) ?? "0"}%`}
          icon={CheckCircle2}
          color="var(--chart-2)"
          tooltip="Percentage of sent emails confirmed delivered by the recipient's mail server."
        />
        <StatCard
          title="Bounce Rate"
          value={`${summary?.bounce_rate.toFixed(2) ?? "0"}%`}
          icon={XCircle}
          color="var(--destructive)"
          tooltip="Percentage of sent emails permanently rejected. Keep below 2% to protect sender reputation."
        />
        <StatCard
          title="Open Rate"
          value={`${summary?.open_rate.toFixed(1) ?? "0"}%`}
          icon={Eye}
          color="var(--primary)"
          tooltip="Percentage of delivered emails where the tracking pixel was loaded. Undercounts due to image blocking."
        />
        <StatCard
          title="Click Rate"
          value={`${summary?.click_rate.toFixed(1) ?? "0"}%`}
          icon={MousePointerClick}
          color="var(--chart-1)"
          tooltip="Percentage of delivered emails where at least one tracked link was clicked."
        />
        <StatCard
          title="Complaints"
          value={summary?.complained ?? 0}
          subtitle={`${summary?.complaint_rate.toFixed(2) ?? "0"}% rate`}
          icon={AlertTriangle}
          color="var(--chart-3)"
          tooltip="Percentage of delivered emails marked as spam by recipients. SES suspends at 0.1%."
        />
      </div>

      {/* Charts + Health Row — hi-fi: 2/3 chart, 1/3 health */}
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          {timelineLoading ? (
            <Skeleton className="h-[340px] rounded-lg bg-muted" />
          ) : (
            <SendVolumeChart data={timeline?.points ?? []} />
          )}
        </div>
        <div>{health && <HealthStatus health={health} />}</div>
      </div>

      {/* Recent Emails — hi-fi: card wrapper with border, shadow-sm */}
      <div className="data-table-wrap">
        <div className="border-b border-border px-[14px] py-[10px]">
          <h3 className="text-sm font-semibold text-foreground">
            Recent Emails
          </h3>
        </div>
        <div>
          {emailsLoading ? (
            <div className="p-6">
              <Skeleton className="h-[200px] bg-muted" />
            </div>
          ) : (
            <EmailTable
              emails={extractItems(emails)}
              total={emails?.totalCount ?? 0}
              page={1}
              pageSize={10}
              totalPages={1}
              onPageChange={() => {}}
              onRowClick={setSelectedEmail}
              compact
            />
          )}
        </div>
      </div>

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
