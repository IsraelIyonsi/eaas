"use client";

import { extractItems } from "@/lib/utils/api-response";
import { PageHeader } from "@/components/shared/page-header";
import { StatCard } from "@/components/overview/stat-card";
import { DataTable } from "@/components/shared/data-table";
import { HealthDot } from "@/components/shared/status-badge";
import { StatCardsSkeleton } from "@/components/shared/loading-skeleton";
import { useAdminPlatformSummary, useAdminTenantRankings } from "@/lib/hooks/use-admin-analytics";
import { useAdminSystemHealth } from "@/lib/hooks/use-admin-health";
import { Building2, Users, Send, Globe, Activity } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { TenantRanking, AdminServiceHealth } from "@/types/admin";
import type { HealthStatus } from "@/types/health";

const tenantColumns = [
  {
    key: "tenantName",
    header: "Tenant",
    render: (item: TenantRanking) => (
      <span className="font-medium text-foreground">{item.tenantName}</span>
    ),
  },
  {
    key: "emailCount",
    header: "Emails Sent",
    render: (item: TenantRanking) => (item.emailCount ?? 0).toLocaleString(),
  },
];

export default function AdminOverviewPage() {
  const { data: summary, isLoading: summaryLoading } = useAdminPlatformSummary();
  const { data: rankings, isLoading: rankingsLoading } = useAdminTenantRankings({ limit: 5 });
  const { data: health, isLoading: healthLoading } = useAdminSystemHealth();

  const topTenants = extractItems(rankings);

  if (summaryLoading) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Admin Overview"
          description="Platform-wide metrics and system health."
        />
        <StatCardsSkeleton count={4} />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Admin Overview"
        description="Platform-wide metrics and system health."
      />

      {/* Stat Cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title="Total Tenants"
          value={summary?.totalTenants ?? 0}
          icon={Building2}
          color="#7c3aed"
        />
        <StatCard
          title="Active Tenants"
          value={summary?.activeTenants ?? 0}
          icon={Users}
          color="var(--chart-2)"
        />
        <StatCard
          title="Total Emails"
          value={(summary?.totalEmails ?? 0).toLocaleString()}
          icon={Send}
          color="var(--chart-1)"
        />
        <StatCard
          title="Total Domains"
          value={summary?.totalDomains ?? 0}
          icon={Globe}
          color="var(--primary)"
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        {/* Top Tenants */}
        <div className="lg:col-span-2">
          <Card className="border-border bg-card shadow-none">
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-semibold text-foreground">
                Top Tenants
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <DataTable<TenantRanking>
                columns={tenantColumns}
                data={topTenants}
                loading={rankingsLoading}
                getRowId={(item) => item.tenantId}
              />
            </CardContent>
          </Card>
        </div>

        {/* System Health */}
        <Card className="border-border bg-card shadow-none">
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-sm font-semibold text-foreground">
              <Activity className="h-4 w-4 text-[#7c3aed]" />
              System Health
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 pt-0">
            {healthLoading ? (
              <div className="space-y-3">
                {Array.from({ length: 4 }).map((_, i) => (
                  <Skeleton key={i} className="h-10 rounded-lg" />
                ))}
              </div>
            ) : (
              health?.services?.map((service: AdminServiceHealth) => (
                <div
                  key={service.name}
                  className="flex items-center justify-between rounded-lg bg-muted px-3 py-2"
                >
                  <span className="text-sm text-foreground/70">{service.name}</span>
                  <div className="flex items-center gap-3">
                    {service.latencyMs !== undefined && (
                      <span className="text-xs text-muted-foreground">
                        {service.latencyMs}ms
                      </span>
                    )}
                    <HealthDot status={service.status as HealthStatus} />
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
