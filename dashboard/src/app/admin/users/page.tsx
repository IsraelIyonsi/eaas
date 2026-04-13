"use client";

import { useState } from "react";
import { extractItems, extractTotalCount } from "@/lib/utils/api-response";
import { PageHeader } from "@/components/shared/page-header";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { AdminRoleBadge } from "@/components/shared/status-badge";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { AdminUserDialog } from "@/components/admin/admin-user-dialog";
import { useAdminUsers, useCreateAdminUser, useDeleteAdminUser } from "@/lib/hooks/use-admin-users";
import { PAGE_SIZE_DEFAULT } from "@/lib/constants/ui";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Users, Plus, Trash2 } from "lucide-react";
import { format, parseISO } from "date-fns";
import { toast } from "sonner";
import type { AdminUser, CreateAdminUserRequest } from "@/types/admin";

const columns = [
  {
    key: "email",
    header: "Email",
    render: (item: AdminUser) => (
      <span className="font-medium text-foreground">{item.email}</span>
    ),
  },
  {
    key: "name",
    header: "Name",
    render: (item: AdminUser) => item.displayName,
  },
  {
    key: "role",
    header: "Role",
    render: (item: AdminUser) => <AdminRoleBadge role={item.role} />,
  },
  {
    key: "isActive",
    header: "Active",
    render: (item: AdminUser) => (
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
  {
    key: "lastLoginAt",
    header: "Last Login",
    render: (item: AdminUser) => (
      <span className="text-muted-foreground">
        {item.lastLoginAt ? format(parseISO(item.lastLoginAt), "MMM d, yyyy HH:mm") : "Never"}
      </span>
    ),
  },
];

export default function AdminUsersPage() {
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<AdminUser | null>(null);

  const { data, isLoading } = useAdminUsers({ page, page_size: PAGE_SIZE_DEFAULT });
  const createMutation = useCreateAdminUser();
  const deleteMutation = useDeleteAdminUser();

  const users = extractItems(data);
  const total = extractTotalCount(data);

  function handleCreate(formData: CreateAdminUserRequest) {
    createMutation.mutate(formData, {
      onSuccess: () => {
        toast.success("Admin user created");
        setCreateOpen(false);
      },
      onError: () => toast.error("Failed to create admin user"),
    });
  }

  function handleDelete() {
    if (!deleteTarget) return;
    deleteMutation.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Admin user deleted");
        setDeleteTarget(null);
      },
      onError: () => toast.error("Failed to delete admin user"),
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Admin Users"
        description="Manage platform administrators."
        badge={total > 0 ? `${total}` : undefined}
        action={
          <Button size="sm" onClick={() => setCreateOpen(true)}>
            <Plus className="mr-1.5 h-3.5 w-3.5" />
            Create User
          </Button>
        }
      />

      <DataTable<AdminUser>
        columns={[
          ...columns,
          {
            key: "actions",
            header: "",
            className: "w-12",
            render: (item: AdminUser) => (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  setDeleteTarget(item);
                }}
                className="rounded p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-destructive"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            ),
          },
        ]}
        data={users}
        total={total}
        page={page}
        pageSize={PAGE_SIZE_DEFAULT}
        onPageChange={setPage}
        loading={isLoading}
        getRowId={(item) => item.id}
        emptyState={
          <EmptyState
            icon={Users}
            title="No admin users"
            description="Create your first admin user to get started."
            action={{ label: "Create User", onClick: () => setCreateOpen(true) }}
          />
        }
      />

      <AdminUserDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        loading={createMutation.isPending}
        onSubmit={handleCreate}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete Admin User"
        description={`Are you sure you want to delete "${deleteTarget?.email ?? ""}"? This action cannot be undone.`}
        confirmLabel="Delete User"
        variant="destructive"
        loading={deleteMutation.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
