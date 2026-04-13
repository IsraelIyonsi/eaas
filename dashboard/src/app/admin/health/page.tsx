"use client";

import { PageHeader } from "@/components/shared/page-header";
import { HealthDot } from "@/components/shared/status-badge";
import { StatCard } from "@/components/overview/stat-card";
import { StatCardsSkeleton } from "@/components/shared/loading-skeleton";
import { useAdminSystemHealth } from "@/lib/hooks/use-admin-health";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Activity, Building2, Send, Layers } from "lucide-react";
import { CHART_COLOR_ADMIN } from "@/lib/constants/ui";
import type { HealthStatus } from "@/types/health";

export default function AdminHealthPage() {
  const { data: health, isLoading } = useAdminSystemHealth();

  if (isLoading) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="System Health"
          description="Monitor platform services and infrastructure."
        />
        <StatCardsSkeleton count={4} />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="System Health"
        description="Monitor platform services and infrastructure."
      />

      {/* Service Status Cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {health?.services?.map((service) => (
          <Card key={service.name} className="border-border bg-card shadow-none">
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-foreground">
                  {service.name}
                </span>
                <HealthDot status={service.status as HealthStatus} />
              </div>
              {service.latencyMs !== undefined && (
                <p className="mt-2 text-xs text-muted-foreground">
                  Latency: {service.latencyMs}ms
                </p>
              )}
              {service.message && (
                <p className="mt-1 text-xs text-muted-foreground">
                  {service.message}
                </p>
              )}
            </CardContent>
          </Card>
        )) ?? (
          <div className="col-span-full">
            <Skeleton className="h-24 rounded-lg" />
          </div>
        )}
      </div>

      {/* Platform Metrics */}
      <Card className="border-border bg-card shadow-none">
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-sm font-semibold text-foreground">
            <Activity className="h-4 w-4 text-[#7c3aed]" />
            Platform Metrics
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard
              title="Tenant Count"
              value={health?.metrics?.tenantCount ?? 0}
              icon={Building2}
              color={CHART_COLOR_ADMIN}
            />
            <StatCard
              title="Total Emails"
              value={(health?.metrics?.totalEmailsSent ?? 0).toLocaleString()}
              icon={Send}
              color="var(--chart-1)"
            />
            <StatCard
              title="Queue Depth"
              value={health?.metrics?.queueDepth ?? 0}
              icon={Layers}
              color="var(--chart-3)"
            />
            <StatCard
              title="Avg Latency"
              value={`${health?.metrics?.avgLatencyMs ?? 0}ms`}
              icon={Activity}
              color="var(--chart-2)"
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
