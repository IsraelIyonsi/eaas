"use client";

import { Key, Globe, Send } from "lucide-react";
import { StatCard } from "@/components/overview/stat-card";
import type { AdminTenant } from "@/types/admin";

interface TenantStatsCardsProps {
  tenant: AdminTenant;
}

export function TenantStatsCards({ tenant }: TenantStatsCardsProps) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <StatCard
        title="API Keys"
        value={tenant.apiKeyCount}
        icon={Key}
        color="var(--primary)"
      />
      <StatCard
        title="Domains"
        value={tenant.domainCount}
        icon={Globe}
        color="var(--chart-2)"
      />
      <StatCard
        title="Emails Sent"
        value={tenant.emailCount.toLocaleString()}
        icon={Send}
        color="var(--chart-1)"
      />
    </div>
  );
}
