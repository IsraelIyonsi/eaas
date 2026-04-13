"use client";

import { useState, useMemo } from "react";
import { PageHeader } from "@/components/shared/page-header";
import { StatCard } from "@/components/overview/stat-card";
import { StatCardsSkeleton } from "@/components/shared/loading-skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  useInboundAnalyticsSummary,
  useInboundAnalyticsTimeline,
  useTopSenders,
} from "@/lib/hooks/use-analytics";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
} from "recharts";
import {
  Inbox,
  CheckCircle2,
  XCircle,
  Clock,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { format, parseISO, subDays, subHours } from "date-fns";
import { INBOUND_STATUS_COLORS } from "@/lib/constants/ui";

const ranges = [
  { label: "24h", hours: 24 },
  { label: "7d", hours: 168 },
  { label: "30d", hours: 720 },
];

export default function InboundAnalyticsPage() {
  const [rangeIdx, setRangeIdx] = useState(1);
  const range = ranges[rangeIdx];

  const dateFrom = useMemo(
    () => subHours(new Date(), range.hours).toISOString(),
    [range.hours],
  );

  const { data: summary, isLoading: summaryLoading } =
    useInboundAnalyticsSummary({ date_from: dateFrom });
  const { data: timeline, isLoading: timelineLoading } =
    useInboundAnalyticsTimeline({
      granularity: range.hours <= 24 ? "hour" : "day",
      date_from: dateFrom,
    });
  const { data: topSenders, isLoading: sendersLoading } = useTopSenders({
    date_from: dateFrom,
    limit: 5,
  });

  const sendersList = Array.isArray(topSenders) ? topSenders : [];

  // Donut chart data
  const donutData = summary
    ? [
        { name: "Processed", value: summary.processed ?? 0, color: INBOUND_STATUS_COLORS.processed },
        { name: "Failed", value: summary.failed ?? 0, color: INBOUND_STATUS_COLORS.failed },
        { name: "Spam", value: summary.spam_flagged ?? 0, color: INBOUND_STATUS_COLORS.spam },
        { name: "Virus", value: summary.virus_flagged ?? 0, color: INBOUND_STATUS_COLORS.virus },
      ]
    : [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Inbound Analytics"
        description="Monitor inbound email volume, processing rates, and top senders."
        action={
          <div className="flex items-center rounded-lg border border-border bg-muted p-0.5">
            {ranges.map((r, i) => (
              <Button
                key={r.label}
                variant="ghost"
                size="sm"
                onClick={() => setRangeIdx(i)}
                className={cn(
                  "h-7 px-3 text-xs",
                  i === rangeIdx
                    ? "bg-primary text-primary-foreground hover:bg-primary/90"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                {r.label}
              </Button>
            ))}
          </div>
        }
      />

      {/* Metric Cards */}
      {summaryLoading ? (
        <StatCardsSkeleton count={4} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <StatCard
            title="Total Received"
            value={summary?.total_received ?? 0}
            icon={Inbox}
            color="var(--primary)"
          />
          <StatCard
            title="Processed"
            value={summary?.processed ?? 0}
            icon={CheckCircle2}
            color={INBOUND_STATUS_COLORS.processed}
            subtitle={
              summary
                ? `${((summary.processing_rate ?? 0) * 100).toFixed(1)}% rate`
                : undefined
            }
          />
          <StatCard
            title="Failed"
            value={summary?.failed ?? 0}
            icon={XCircle}
            color={INBOUND_STATUS_COLORS.failed}
          />
          <StatCard
            title="Avg Processing"
            value={
              summary
                ? `${summary.avg_processing_time_ms ?? 0}ms`
                : "0ms"
            }
            icon={Clock}
            color={INBOUND_STATUS_COLORS.spam}
          />
        </div>
      )}

      {/* Volume Chart */}
      <Card className="border-border bg-card shadow-none">
        <CardHeader>
          <CardTitle className="text-sm font-semibold text-foreground">
            Inbound Volume
          </CardTitle>
        </CardHeader>
        <CardContent>
          {timelineLoading ? (
            <Skeleton className="h-[250px] bg-muted" />
          ) : (
            <ResponsiveContainer width="100%" height={250}>
              <AreaChart
                data={timeline?.points ?? []}
                margin={{ top: 5, right: 5, left: -15, bottom: 0 }}
              >
                <defs>
                  <linearGradient id="inboundGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="var(--primary)" stopOpacity={0.3} />
                    <stop offset="100%" stopColor="var(--primary)" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" opacity={0.5} />
                <XAxis
                  dataKey="timestamp"
                  tickFormatter={(v: string) =>
                    range.hours <= 24
                      ? format(parseISO(v), "HH:mm")
                      : format(parseISO(v), "MMM d")
                  }
                  stroke="var(--border)"
                  tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                />
                <YAxis
                  stroke="var(--border)"
                  tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                />
                <RechartsTooltip
                  contentStyle={{
                    backgroundColor: "var(--popover)",
                    border: "1px solid var(--border)",
                    borderRadius: 8,
                    color: "var(--popover-foreground)",
                    fontSize: 12,
                  }}
                  labelFormatter={(v) =>
                    format(parseISO(String(v)), "MMM d, HH:mm")
                  }
                />
                <Area
                  type="monotone"
                  dataKey="sent"
                  stroke="var(--primary)"
                  fill="url(#inboundGrad)"
                  strokeWidth={2}
                  name="Received"
                />
              </AreaChart>
            </ResponsiveContainer>
          )}
        </CardContent>
      </Card>

      {/* Bottom row: Status breakdown + Top senders */}
      <div className="grid gap-4 lg:grid-cols-2">
        {/* Donut */}
        <Card className="border-border bg-card shadow-none">
          <CardHeader>
            <CardTitle className="text-sm font-semibold text-foreground">
              Status Breakdown
            </CardTitle>
          </CardHeader>
          <CardContent>
            {summaryLoading ? (
              <Skeleton className="mx-auto h-[200px] w-[200px] rounded-full bg-muted" />
            ) : (
              <div className="flex items-center justify-center gap-8">
                <ResponsiveContainer width={200} height={200}>
                  <PieChart>
                    <Pie
                      data={donutData}
                      cx="50%"
                      cy="50%"
                      innerRadius={55}
                      outerRadius={80}
                      paddingAngle={3}
                      dataKey="value"
                    >
                      {donutData.map((entry) => (
                        <Cell key={entry.name} fill={entry.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip
                      contentStyle={{
                        backgroundColor: "var(--popover)",
                        border: "1px solid var(--border)",
                        borderRadius: 8,
                        color: "var(--popover-foreground)",
                        fontSize: 12,
                      }}
                    />
                  </PieChart>
                </ResponsiveContainer>
                <div className="space-y-2">
                  {donutData.map((d) => (
                    <div key={d.name} className="flex items-center gap-2 text-xs">
                      <span
                        className="inline-block h-2.5 w-2.5 rounded-full"
                        style={{ backgroundColor: d.color }}
                      />
                      <span className="text-muted-foreground">{d.name}</span>
                      <span className="font-medium text-foreground">{d.value}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Top Senders */}
        <Card className="border-border bg-card shadow-none">
          <CardHeader>
            <CardTitle className="text-sm font-semibold text-foreground">
              Top Senders
            </CardTitle>
          </CardHeader>
          <CardContent>
            {sendersLoading ? (
              <Skeleton className="h-[200px] bg-muted" />
            ) : sendersList.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted-foreground/60">
                No sender data available
              </p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow className="border-border hover:bg-transparent">
                    <TableHead className="text-[10px] uppercase tracking-wider text-muted-foreground/60">
                      Email
                    </TableHead>
                    <TableHead className="text-right text-[10px] uppercase tracking-wider text-muted-foreground/60">
                      Count
                    </TableHead>
                    <TableHead className="text-right text-[10px] uppercase tracking-wider text-muted-foreground/60">
                      Last Seen
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sendersList.map((s) => (
                    <TableRow
                      key={s.email}
                      className="border-border hover:bg-muted"
                    >
                      <TableCell className="font-mono text-xs text-foreground/80">
                        {s.email}
                      </TableCell>
                      <TableCell className="text-right text-xs font-medium text-foreground">
                        {s.total_emails}
                      </TableCell>
                      <TableCell className="text-right text-xs text-muted-foreground/60">
                        {format(parseISO(s.last_receivedAt), "MMM d, HH:mm")}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
