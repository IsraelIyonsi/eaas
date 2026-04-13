"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { extractItems, extractTotalCount } from "@/lib/utils/api-response";
import { PageHeader } from "@/components/shared/page-header";
import { FilterBar } from "@/components/shared/filter-bar";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { TenantStatusBadge } from "@/components/shared/status-badge";
import { useAdminTenants } from "@/lib/hooks/use-admin-tenants";
import { TenantStatusConfig } from "@/lib/constants/status";
import { Routes } from "@/lib/constants/routes";
import { PAGE_SIZE_DEFAULT } from "@/lib/constants/ui";
import { Building2 } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { AdminTenant, TenantStatus } from "@/types/admin";

const statusOptions = [
  { value: "all", label: "All Statuses" },
  ...Object.entries(TenantStatusConfig).map(([value, config]) => ({
    value,
    label: config.label,
  })),
];

const columns = [
  {
    key: "name",
    header: "Name",
    render: (item: AdminTenant) => (
      <span className="font-medium text-foreground">{item.name}</span>
    ),
  },
  {
    key: "status",
    header: "Status",
    render: (item: AdminTenant) => (
      <TenantStatusBadge status={item.status} />
    ),
  },
  {
    key: "company",
    header: "Company",
    render: (item: AdminTenant) => (
      <span className="text-muted-foreground">{item.company ?? "-"}</span>
    ),
  },
  {
    key: "emailCount",
    header: "Emails",
    render: (item: AdminTenant) => item.emailCount.toLocaleString(),
  },
  {
    key: "createdAt",
    header: "Created",
    render: (item: AdminTenant) => (
      <span className="text-muted-foreground">
        {format(parseISO(item.createdAt), "MMM d, yyyy")}
      </span>
    ),
  },
];

export default function AdminTenantsPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("all");
  const [search, setSearch] = useState("");

  const { data, isLoading } = useAdminTenants({
    page,
    page_size: PAGE_SIZE_DEFAULT,
    status: status === "all" ? undefined : (status as TenantStatus),
    search: search || undefined,
  });

  const tenants = extractItems(data);
  const total = extractTotalCount(data);

  function clearFilters() {
    setStatus("all");
    setSearch("");
    setPage(1);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Tenants"
        description="Manage all tenants on the platform."
        badge={total > 0 ? `${total}` : undefined}
      />

      <FilterBar
        search={{
          value: search,
          onChange: (v) => {
            setSearch(v);
            setPage(1);
          },
          placeholder: "Search by name, company...",
        }}
        filters={[
          {
            key: "status",
            label: "Status",
            type: "select",
            options: statusOptions,
            value: status,
            onChange: (v) => {
              setStatus((v as string) ?? "all");
              setPage(1);
            },
          },
        ]}
        onClear={clearFilters}
      />

      <DataTable<AdminTenant>
        columns={columns}
        data={tenants}
        total={total}
        page={page}
        pageSize={PAGE_SIZE_DEFAULT}
        onPageChange={setPage}
        onRowClick={(tenant) => router.push(Routes.ADMIN_TENANT_DETAIL(tenant.id))}
        loading={isLoading}
        getRowId={(item) => item.id}
        emptyState={
          <EmptyState
            icon={Building2}
            title="No tenants found"
            description="No tenants match your current filters."
          />
        }
      />
    </div>
  );
}
