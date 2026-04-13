"use client";

import { useState } from "react";
import { extractItems, extractTotalCount } from "@/lib/utils/api-response";
import { PageHeader } from "@/components/shared/page-header";
import { FilterBar } from "@/components/shared/filter-bar";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { useAdminAuditLogs } from "@/lib/hooks/use-admin-audit-logs";
import { PAGE_SIZE_DEFAULT } from "@/lib/constants/ui";
import { ScrollText } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { AuditLog } from "@/types/admin";

const actionOptions = [
  { value: "all", label: "All Actions" },
  { value: "tenant.create", label: "Tenant Create" },
  { value: "tenant.update", label: "Tenant Update" },
  { value: "tenant.suspend", label: "Tenant Suspend" },
  { value: "tenant.activate", label: "Tenant Activate" },
  { value: "tenant.delete", label: "Tenant Delete" },
  { value: "user.create", label: "User Create" },
  { value: "user.delete", label: "User Delete" },
  { value: "config.update", label: "Config Update" },
];

const columns = [
  {
    key: "createdAt",
    header: "Timestamp",
    render: (item: AuditLog) => (
      <span className="text-muted-foreground">
        {format(parseISO(item.createdAt), "MMM d, yyyy HH:mm:ss")}
      </span>
    ),
  },
  {
    key: "adminEmail",
    header: "Admin",
    render: (item: AuditLog) => (
      <span className="font-medium text-foreground">{item.adminEmail}</span>
    ),
  },
  {
    key: "action",
    header: "Action",
    render: (item: AuditLog) => (
      <span className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs text-foreground">
        {item.action}
      </span>
    ),
  },
  {
    key: "target",
    header: "Target",
    render: (item: AuditLog) => (
      <span className="text-muted-foreground">
        {item.targetName ?? item.targetType}
        {item.targetId ? ` (${item.targetId.slice(0, 8)}...)` : ""}
      </span>
    ),
  },
  {
    key: "ipAddress",
    header: "IP",
    render: (item: AuditLog) => (
      <span className="font-mono text-xs text-muted-foreground">{item.ipAddress}</span>
    ),
  },
];

export default function AdminAuditLogsPage() {
  const [page, setPage] = useState(1);
  const [action, setAction] = useState("all");

  const { data, isLoading } = useAdminAuditLogs({
    page,
    page_size: PAGE_SIZE_DEFAULT,
    action: action === "all" ? undefined : action,
  });

  const logs = extractItems(data);
  const total = extractTotalCount(data);

  function clearFilters() {
    setAction("all");
    setPage(1);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Audit Logs"
        description="Track all administrative actions on the platform."
        badge={total > 0 ? `${total}` : undefined}
      />

      <FilterBar
        filters={[
          {
            key: "action",
            label: "Action",
            type: "select",
            options: actionOptions,
            value: action,
            onChange: (v) => {
              setAction((v as string) ?? "all");
              setPage(1);
            },
          },
        ]}
        onClear={clearFilters}
      />

      <DataTable<AuditLog>
        columns={columns}
        data={logs}
        total={total}
        page={page}
        pageSize={PAGE_SIZE_DEFAULT}
        onPageChange={setPage}
        loading={isLoading}
        getRowId={(item) => item.id}
        emptyState={
          <EmptyState
            icon={ScrollText}
            title="No audit logs"
            description="Administrative actions will be logged here."
          />
        }
      />
    </div>
  );
}
