"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { PageHeader } from "@/components/shared/page-header";
import { DetailSkeleton } from "@/components/shared/loading-skeleton";
import { TenantStatusBadge } from "@/components/shared/status-badge";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { TenantFormSheet } from "@/components/admin/tenant-form-sheet";
import { TenantStatsCards } from "@/components/admin/tenant-stats-cards";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  useAdminTenant,
  useSuspendTenant,
  useActivateTenant,
  useDeleteTenant,
  useUpdateTenant,
} from "@/lib/hooks/use-admin-tenants";
import { Routes } from "@/lib/constants/routes";
import { toast } from "sonner";
import { Pencil, Trash2, Pause, Play, Loader2 } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { UpdateTenantRequest } from "@/types/admin";

export default function AdminTenantDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const { data: tenant, isLoading } = useAdminTenant(id);
  const suspendMutation = useSuspendTenant();
  const activateMutation = useActivateTenant();
  const deleteMutation = useDeleteTenant();
  const updateMutation = useUpdateTenant();

  const [deleteOpen, setDeleteOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);

  if (isLoading || !tenant) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Tenant Details"
          backHref={Routes.ADMIN_TENANTS}
          backLabel="Back to Tenants"
        />
        <DetailSkeleton />
      </div>
    );
  }

  function handleSuspend() {
    suspendMutation.mutate(id, {
      onSuccess: () => toast.success("Tenant suspended"),
      onError: () => toast.error("Failed to suspend tenant"),
    });
  }

  function handleActivate() {
    activateMutation.mutate(id, {
      onSuccess: () => toast.success("Tenant activated"),
      onError: () => toast.error("Failed to activate tenant"),
    });
  }

  function handleDelete() {
    deleteMutation.mutate(id, {
      onSuccess: () => {
        toast.success("Tenant deleted");
        router.push(Routes.ADMIN_TENANTS);
      },
      onError: () => toast.error("Failed to delete tenant"),
    });
  }

  function handleUpdate(data: UpdateTenantRequest) {
    updateMutation.mutate(
      { id, data },
      {
        onSuccess: () => {
          toast.success("Tenant updated");
          setEditOpen(false);
        },
        onError: () => toast.error("Failed to update tenant"),
      },
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={tenant.name}
        backHref={Routes.ADMIN_TENANTS}
        backLabel="Back to Tenants"
        action={<TenantStatusBadge status={tenant.status} />}
      />

      {/* Tenant Info */}
      <Card className="border-border bg-card shadow-none">
        <CardContent className="p-5">
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Company</p>
              <p className="text-sm text-foreground">{tenant.company ?? "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Email</p>
              <p className="text-sm text-foreground">{tenant.email}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Created</p>
              <p className="text-sm text-foreground">
                {format(parseISO(tenant.createdAt), "MMM d, yyyy")}
              </p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Daily Limit</p>
              <p className="text-sm text-foreground">
                {tenant.dailyEmailLimit.toLocaleString()}
              </p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Monthly Limit</p>
              <p className="text-sm text-foreground">
                {tenant.monthlyEmailLimit.toLocaleString()}
              </p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Status</p>
              <TenantStatusBadge status={tenant.status} />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Action Buttons */}
      <div className="flex flex-wrap gap-2">
        {tenant.status === "active" ? (
          <Button
            variant="outline"
            size="sm"
            onClick={handleSuspend}
            disabled={suspendMutation.isPending}
          >
            {suspendMutation.isPending ? (
              <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
            ) : (
              <Pause className="mr-1.5 h-3.5 w-3.5" />
            )}
            Suspend
          </Button>
        ) : (
          <Button
            variant="outline"
            size="sm"
            onClick={handleActivate}
            disabled={activateMutation.isPending}
          >
            {activateMutation.isPending ? (
              <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
            ) : (
              <Play className="mr-1.5 h-3.5 w-3.5" />
            )}
            Activate
          </Button>
        )}
        <Button
          variant="outline"
          size="sm"
          onClick={() => setEditOpen(true)}
        >
          <Pencil className="mr-1.5 h-3.5 w-3.5" />
          Edit
        </Button>
        <Button
          variant="destructive"
          size="sm"
          onClick={() => setDeleteOpen(true)}
        >
          <Trash2 className="mr-1.5 h-3.5 w-3.5" />
          Delete
        </Button>
      </div>

      {/* Stats Cards */}
      <TenantStatsCards tenant={tenant} />

      {/* Edit Sheet */}
      <TenantFormSheet
        open={editOpen}
        onOpenChange={setEditOpen}
        tenant={tenant}
        loading={updateMutation.isPending}
        onSubmit={handleUpdate}
      />

      {/* Delete Confirm */}
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete Tenant"
        description={`Are you sure you want to delete "${tenant.name}"? This will permanently remove all their data including emails, domains, and API keys. This cannot be undone.`}
        confirmLabel="Delete Tenant"
        variant="destructive"
        loading={deleteMutation.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
