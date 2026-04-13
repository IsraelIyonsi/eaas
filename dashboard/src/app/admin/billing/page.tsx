"use client";

import { useState } from "react";
import { extractItems, extractTotalCount } from "@/lib/utils/api-response";
import { PageHeader } from "@/components/shared/page-header";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { PlanDialog } from "@/components/admin/plan-dialog";
import { useAdminPlans, useCreatePlan, useUpdatePlan } from "@/lib/hooks/use-admin-billing";
import { PAGE_SIZE_DEFAULT } from "@/lib/constants/ui";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { CreditCard, Plus, Pencil } from "lucide-react";
import { toast } from "sonner";
import type { Plan, CreatePlanRequest, UpdatePlanRequest } from "@/types/billing";

function formatPrice(amount: number): string {
  return `$${amount.toFixed(2)}`;
}

function formatNumber(n: number): string {
  return n.toLocaleString();
}

const tierColors: Record<string, string> = {
  free: "bg-gray-500/15 text-gray-400 border-gray-500/30",
  starter: "bg-blue-500/15 text-blue-400 border-blue-500/30",
  pro: "bg-violet-500/15 text-violet-400 border-violet-500/30",
  business: "bg-amber-500/15 text-amber-400 border-amber-500/30",
  enterprise: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
};

const columns = [
  {
    key: "name",
    header: "Name",
    render: (item: Plan) => (
      <span className="font-medium text-foreground">{item.name}</span>
    ),
  },
  {
    key: "tier",
    header: "Tier",
    render: (item: Plan) => (
      <Badge
        variant="outline"
        className={`text-xs capitalize ${tierColors[item.tier] ?? "bg-gray-500/15 text-gray-400 border-gray-500/30"}`}
      >
        {item.tier}
      </Badge>
    ),
  },
  {
    key: "monthlyPriceUsd",
    header: "Monthly Price",
    render: (item: Plan) => (
      <span className="text-muted-foreground">{formatPrice(item.monthlyPriceUsd)}</span>
    ),
  },
  {
    key: "limits",
    header: "Limits",
    render: (item: Plan) => (
      <span className="text-xs text-muted-foreground">
        {formatNumber(item.dailyEmailLimit)}/day, {formatNumber(item.monthlyEmailLimit)}/mo
      </span>
    ),
  },
  {
    key: "isActive",
    header: "Active",
    render: (item: Plan) => (
      <Badge
        variant="outline"
        className={
          item.isActive
            ? "bg-emerald-500/15 text-emerald-400 border-emerald-500/30 text-xs"
            : "bg-gray-500/15 text-gray-400 border-gray-500/30 text-xs"
        }
      >
        {item.isActive ? "Yes" : "No"}
      </Badge>
    ),
  },
];

export default function AdminBillingPage() {
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Plan | null>(null);

  const { data, isLoading } = useAdminPlans({ page, page_size: PAGE_SIZE_DEFAULT });
  const createMutation = useCreatePlan();
  const updateMutation = useUpdatePlan();

  const plans = extractItems(data);
  const total = extractTotalCount(data);

  function handleCreate(formData: CreatePlanRequest) {
    createMutation.mutate(formData, {
      onSuccess: () => {
        toast.success("Billing plan created");
        setCreateOpen(false);
      },
      onError: () => toast.error("Failed to create billing plan"),
    });
  }

  function handleUpdate(formData: CreatePlanRequest) {
    if (!editTarget) return;
    const updateData: UpdatePlanRequest = { ...formData, isActive: editTarget.isActive };
    updateMutation.mutate(
      { id: editTarget.id, data: updateData },
      {
        onSuccess: () => {
          toast.success("Billing plan updated");
          setEditTarget(null);
        },
        onError: () => toast.error("Failed to update billing plan"),
      },
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Billing Plans"
        description="Manage platform billing plans and pricing."
        badge={total > 0 ? `${total}` : undefined}
        action={
          <Button size="sm" onClick={() => setCreateOpen(true)}>
            <Plus className="mr-1.5 h-3.5 w-3.5" />
            Create Plan
          </Button>
        }
      />

      <DataTable<Plan>
        columns={[
          ...columns,
          {
            key: "actions",
            header: "",
            className: "w-12",
            render: (item: Plan) => (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  setEditTarget(item);
                }}
                className="rounded p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
              >
                <Pencil className="h-4 w-4" />
              </button>
            ),
          },
        ]}
        data={plans}
        total={total}
        page={page}
        pageSize={PAGE_SIZE_DEFAULT}
        onPageChange={setPage}
        loading={isLoading}
        getRowId={(item) => item.id}
        emptyState={
          <EmptyState
            icon={CreditCard}
            title="No billing plans"
            description="Create your first billing plan to get started."
            action={{ label: "Create Plan", onClick: () => setCreateOpen(true) }}
          />
        }
      />

      <PlanDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        loading={createMutation.isPending}
        onSubmit={handleCreate}
      />

      <PlanDialog
        open={!!editTarget}
        onOpenChange={(open) => !open && setEditTarget(null)}
        loading={updateMutation.isPending}
        onSubmit={handleUpdate}
        plan={editTarget}
      />
    </div>
  );
}
