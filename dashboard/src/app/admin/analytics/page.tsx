"use client";

import { extractItems } from "@/lib/utils/api-response";
import { PageHeader } from "@/components/shared/page-header";
import { StatCard } from "@/components/overview/stat-card";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { StatCardsSkeleton } from "@/components/shared/loading-skeleton";
import {
  useAdminPlatformSummary,
  useAdminTenantRankings,
  useAdminGrowthMetrics,
} from "@/lib/hooks/use-admin-analytics";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Building2, Send, TrendingUp, BarChart3 } from "lucide-react";
import { CHART_COLOR_ADMIN } from "@/lib/constants/ui";
import type { TenantRanking } from "@/types/admin";

const rankingColumns = [
  {
    key: "tenantName",
    header: "Tenant",
    render: (item: TenantRanking) => (
      <span className="font-medium text-foreground">{item.tenantName}</span>
    ),
  },
  {
    key: "company",
    header: "Company",
    render: (item: TenantRanking) => (
      <span className="text-muted-foreground">{item.company ?? "-"}</span>
    ),
  },
  {
    key: "emailCount",
    header: "Emails",
    render: (item: TenantRanking) => item.emailCount.toLocaleString(),
  },
  {
    key: "domainCount",
    header: "Domains",
    render: (item: TenantRanking) => item.domainCount,
  },
];

export default function AdminAnalyticsPage() {
  const { data: summary, isLoading: summaryLoading } = useAdminPlatformSummary();
  const { data: rankings, isLoading: rankingsLoading } = useAdminTenantRankings({ limit: 10 });
  const { data: growth, isLoading: growthLoading } = useAdminGrowthMetrics();

  const topTenants = extractItems(rankings) ?? [];
  const tenantGrowth = growth?.tenantGrowthPercent ?? 0;
  const emailGrowth = growth?.emailGrowthPercent ?? 0;

  if (summaryLoading || growthLoading) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Cross-Tenant Analytics"
          description="Platform-wide email and tenant metrics."
        />
        <StatCardsSkeleton count={4} />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Cross-Tenant Analytics"
        description="Platform-wide email and tenant metrics."
      />

      {/* Summary Metrics */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title="Total Tenants"
          value={summary?.totalTenants ?? 0}
          icon={Building2}
          color={CHART_COLOR_ADMIN}
        />
        <StatCard
          title="Total Emails"
          value={(summary?.totalEmails ?? 0).toLocaleString()}
          icon={Send}
          color="var(--chart-1)"
        />
        <StatCard
          title="Tenant Growth"
          value={`${tenantGrowth}%`}
          icon={TrendingUp}
          color="var(--chart-2)"
          trend={tenantGrowth > 0 ? "up" : tenantGrowth < 0 ? "down" : "flat"}
          trendValue={`${growth?.newTenantsThisMonth ?? 0} this month`}
        />
        <StatCard
          title="Email Growth"
          value={`${emailGrowth}%`}
          icon={TrendingUp}
          color="var(--chart-2)"
          trend={emailGrowth > 0 ? "up" : emailGrowth < 0 ? "down" : "flat"}
          trendValue="vs last month"
        />
      </div>

      {/* Top Tenants Ranking */}
      <Card className="border-border bg-card shadow-none">
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-semibold text-foreground">
            Top Tenants by Email Volume
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <DataTable<TenantRanking>
            columns={rankingColumns}
            data={topTenants}
            loading={rankingsLoading}
            getRowId={(item) => item.tenantId}
            emptyState={
              <EmptyState
                icon={BarChart3}
                title="No data yet"
                description="Tenant rankings will appear once tenants start sending emails."
              />
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}
